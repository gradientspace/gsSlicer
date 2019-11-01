using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public class PlanarSlicePro : PlanarSlice
    {
        public static PlanarSlice FactoryF(Interval1d ZSpan, double Zheight, int idx)
        { 
            return new PlanarSlicePro() { LayerZSpan = ZSpan, Z = Zheight, LayerIndex = idx };
        }


        public Dictionary<GeneralPolygon2d, double> Clearances = new Dictionary<GeneralPolygon2d, double>();
        public Dictionary<GeneralPolygon2d, double> Offsets = new Dictionary<GeneralPolygon2d, double>();


        public Dictionary<GeneralPolygon2d, double> Cavity_Clearances = new Dictionary<GeneralPolygon2d, double>();
        public Dictionary<GeneralPolygon2d, double> Cavity_Offsets = new Dictionary<GeneralPolygon2d, double>();


        Dictionary<GeneralPolygon2d, List<GeneralPolygon2d>> Thickened;


        protected override void transfer_tags(GeneralPolygon2d oldPoly, GeneralPolygon2d newPoly)
        {
            base.transfer_tags(oldPoly, newPoly);

            double clearance;
            if (Clearances.TryGetValue(oldPoly, out clearance))
                Clearances[newPoly] = clearance;
            double offset;
            if (Offsets.TryGetValue(oldPoly, out offset))
                Offsets[newPoly] = offset;
        }


        protected override GeneralPolygon2d[] process_input_polys_before_sort(GeneralPolygon2d[] polys)
        {
            if (Offsets.Count == 0)
                return polys;
            List<GeneralPolygon2d> newPolys = new List<GeneralPolygon2d>();
            bool modified = false;
            foreach ( var poly in polys ) {
                double offset;
                if (Offsets.TryGetValue(poly, out offset) && Math.Abs(offset) > MathUtil.ZeroTolerancef) {
                    List<GeneralPolygon2d> offsetPolys = ClipperUtil.MiterOffset(poly, offset);
                    foreach (var newpoly in offsetPolys) {
                        transfer_tags(poly, newpoly);
                        newPolys.Add(newpoly);
                    }
                    modified = true;
                } else
                    newPolys.Add(poly);
            }
            if (modified == false)
                return polys;
            return newPolys.ToArray();
        }


        protected override GeneralPolygon2d[] process_input_polys_after_sort(GeneralPolygon2d[] solids)
        {
            // construct thickened solids
            Thickened = new Dictionary<GeneralPolygon2d, List<GeneralPolygon2d>>();
            for (int k = 0; k < solids.Length; ++k) {
                double clearance;
                if (Clearances.TryGetValue(solids[k], out clearance) && clearance > 0)
                    Thickened.Add(solids[k], ClipperUtil.MiterOffset(solids[k], clearance));
            }

            return solids;
        }


        protected override List<GeneralPolygon2d> make_solid(GeneralPolygon2d poly, bool bIsSupportSolid)
        {
            List<GeneralPolygon2d> solid = base.make_solid(poly, bIsSupportSolid);
            if (bIsSupportSolid == false && Thickened != null) {

                // subtract clearance solids
                foreach (var pair in Thickened) {
                    if (pair.Key != poly)
                        solid = ClipperUtil.Difference(solid, pair.Value);
                }

            }
            return solid;
        }



        protected override List<GeneralPolygon2d> remove_cavity(List<GeneralPolygon2d> solids, GeneralPolygon2d cavity)
        {
            double offset = 0;
            if (Cavity_Clearances.ContainsKey(cavity) ) {
                offset = Cavity_Clearances[cavity];
            }
            if ( Cavity_Offsets.ContainsKey(cavity) ) {
                offset += Cavity_Offsets[cavity];
            }
            if (Math.Abs(offset) > 0.0001 ) {
                var offset_cavities = ClipperUtil.MiterOffset(cavity, offset, MIN_AREA);
                return ClipperUtil.Difference(solids, offset_cavities, MIN_AREA);
            } else {
                return ClipperUtil.Difference(solids, cavity, MIN_AREA);
            }
        }



    }
}

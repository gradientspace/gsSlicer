using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;

namespace gs
{


    public class ExtendedPrintMeshOptions
    {
        public double ClearanceXY = 0;
        public double OffsetXY = 0;
    }



    public class MeshPlanarSlicerPro : MeshPlanarSlicer
    {
        public MeshPlanarSlicerPro() : base()
        {
        }


        protected override void add_solid_polygons(PlanarSlice slice, List<GeneralPolygon2d> polygons, PrintMeshOptions options)
        {
            slice.AddPolygons(polygons);

            if (options.Extended != null && options.Extended is ExtendedPrintMeshOptions) {
                ExtendedPrintMeshOptions ext = options.Extended as ExtendedPrintMeshOptions;

                if (slice is PlanarSlicePro) {
                    PlanarSlicePro sp = slice as PlanarSlicePro;

                    if (ext.ClearanceXY != 0) {
                        foreach (var poly in polygons)
                            sp.Clearances.Add(poly, ext.ClearanceXY);
                    }

                    if (ext.OffsetXY != 0) {
                        foreach (var poly in polygons)
                            sp.Offsets.Add(poly, ext.OffsetXY);
                    }
                }
            }
        }




        protected override void add_cavity_polygons(PlanarSlice slice, List<GeneralPolygon2d> polygons, PrintMeshOptions options)
        {
            slice.AddCavityPolygons(polygons);

            if (options.Extended != null && options.Extended is ExtendedPrintMeshOptions) {
                ExtendedPrintMeshOptions ext = options.Extended as ExtendedPrintMeshOptions;

                if (slice is PlanarSlicePro) {
                    PlanarSlicePro sp = slice as PlanarSlicePro;

                    if (ext.ClearanceXY != 0) {
                        foreach (var poly in polygons)
                            sp.Cavity_Clearances.Add(poly, ext.ClearanceXY);
                    }

                    if (ext.OffsetXY != 0) {
                        foreach (var poly in polygons)
                            sp.Cavity_Offsets.Add(poly, ext.OffsetXY);
                    }
                }
            }
        }


    }
}

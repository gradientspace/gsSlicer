using System;
using System.Collections.Generic;
using g3;

namespace gs
{
    public class CorrugatedFillPolygon : ICurvesFillPolygon
    {
        // polygon to fill
        public GeneralPolygon2d Polygon { get; set; }

        // parameters
        public double ToolWidth = 0.4;
        public double PathSpacing = 0.4;
        public double AngleDeg = 45.0;
        public double PathShift = 0;

        // if true, we inset half of tool-width from Polygon
        public bool InsetFromInputPolygon = true;

        // fill paths
        public List<FillCurveSet2d> Paths { get; set; }
        public List<FillCurveSet2d> GetFillCurves() { return Paths; }


        SegmentSet2d BoundaryPolygonCache;

        public CorrugatedFillPolygon(GeneralPolygon2d poly)
        {
            Polygon = poly;
            Paths = new List<FillCurveSet2d>();
        }


        public bool Compute()
        {
            if (InsetFromInputPolygon) {
                BoundaryPolygonCache = new SegmentSet2d(Polygon);
                List<GeneralPolygon2d> current = ClipperUtil.ComputeOffsetPolygon(Polygon, -ToolWidth / 2, true);
                foreach (GeneralPolygon2d poly in current) {
                    SegmentSet2d polyCache = new SegmentSet2d(poly);
                    Paths.Add(ComputeFillPaths(poly, polyCache));
                }

            } else {
                List<GeneralPolygon2d> boundary = ClipperUtil.ComputeOffsetPolygon(Polygon, ToolWidth / 2, true);
                BoundaryPolygonCache = new SegmentSet2d(boundary);

                SegmentSet2d polyCache = new SegmentSet2d(Polygon);
                Paths.Add(ComputeFillPaths(Polygon, polyCache));

            }


            return true;
        }




        protected FillCurveSet2d ComputeFillPaths(GeneralPolygon2d poly, SegmentSet2d polyCache)
        {
            return null;
        }

        

    }
}

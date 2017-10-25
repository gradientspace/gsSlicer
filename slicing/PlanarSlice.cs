﻿using System;
using System.Collections.Generic;
using g3;

namespace gs
{
    
	public class PlanarSlice
	{
		public double Z = 0;

        public double EmbeddedPathWidth = 0;

        public List<GeneralPolygon2d> InputSolids = new List<GeneralPolygon2d>();
        public List<PolyLine2d> EmbeddedPaths = new List<PolyLine2d>();
        public List<PolyLine2d> ClippedPaths = new List<PolyLine2d>();

        public List<GeneralPolygon2d> Solids = new List<GeneralPolygon2d>();
        public List<PolyLine2d> Paths = new List<PolyLine2d>();



        // allow integer tags on polygons, which we can use for arbitrary stuff
        public IntTagSet<GeneralPolygon2d> Tags {
            get {
                if (tags == null)
                    tags = new IntTagSet<GeneralPolygon2d>();
                return tags;
            }
        }
        IntTagSet<GeneralPolygon2d> tags;


		public PlanarSlice()
		{
		}


        public bool IsEmpty {
            get { return Solids.Count == 0 && Paths.Count == 0; }
        }


		public void AddPolygon(GeneralPolygon2d poly) {
            if (poly.Outer.IsClockwise)
                poly.Reverse();
            InputSolids.Add(poly);
		}
		public void AddPolygons(IEnumerable<GeneralPolygon2d> polys) {
			foreach (GeneralPolygon2d p in polys)
				AddPolygon(p);
		}


        public void AddEmbeddedPath(PolyLine2d pline) {
            EmbeddedPaths.Add(pline);
        }
        public void AddClippedPath(PolyLine2d pline) {
            ClippedPaths.Add(pline);
        }


        /// <summary>
        /// Convert assembly of polygons, polylines, etc, into a set of printable solids and paths
        /// </summary>
        public void Resolve()
        {
            // sort solids by 
            if (InputSolids.Count > 0) {
                GeneralPolygon2d[] solids = InputSolids.ToArray();
                double[] weights = new double[solids.Length];
                for (int i = 0; i < solids.Length; ++i) {
                    double w = Math.Abs(solids[i].Outer.SignedArea);
                    weights[i] = w;
                }
                Array.Sort(weights, solids); Array.Reverse(solids);

                Solids = new List<GeneralPolygon2d>() { solids[0] };
                for (int k = 1; k < solids.Length; ++k)
                    Solids = ClipperUtil.PolygonBoolean(Solids, solids[k], ClipperUtil.BooleanOp.Union);
            }

            if (EmbeddedPaths.Count > 0 && EmbeddedPathWidth == 0)
                throw new Exception("PlanarSlice.Resolve: must set embedded path width!");
            foreach ( var path in EmbeddedPaths ) {

                PolyLine2d pos = new PolyLine2d(path), neg = new PolyLine2d(path);
                pos.VertexOffset(EmbeddedPathWidth / 2);
                neg.VertexOffset(-EmbeddedPathWidth / 2); neg.Reverse();
                pos.AppendVertices(neg);
                Polygon2d poly = new Polygon2d(pos.Vertices);
                if (poly.IsClockwise)
                    poly.Reverse();

                Solids = ClipperUtil.Difference(Solids, new GeneralPolygon2d(poly));

                Paths.Add(path);
            }

            foreach ( var path in ClippedPaths ) {
                List<PolyLine2d> clipped = ClipperUtil.ClipAgainstPolygon(Solids, path);
                foreach ( var cp in clipped)
                    Paths.Add(cp);
            }


        }



        public AxisAlignedBox2d Bounds {
			get {
				AxisAlignedBox2d box = AxisAlignedBox2d.Empty;
				foreach (GeneralPolygon2d poly in InputSolids)
					box.Contain(poly.Outer.Bounds);
                foreach (PolyLine2d pline in EmbeddedPaths)
                    box.Contain(pline.Bounds);
                foreach (PolyLine2d pline in ClippedPaths)
                    box.Contain(pline.Bounds);
                return box;
			}
		}
	}
}

﻿using System;
using System.Collections.Generic;
using System.IO;
using g3;

namespace gs
{
    
    /// <summary>
    /// Geometry of a 2D slice at given .Z height
    /// </summary>
	public class PlanarSlice
	{
		public double Z = 0;

        public double EmbeddedPathWidth = 0;

        /*
         * Input geometry
         *    - "solid" polygons-with-holes
         *    - embedded paths cut into solids
         *    - clipped paths are clipped against solids
         *    - support solids are clipped against solids
         */

        public List<GeneralPolygon2d> InputSolids = new List<GeneralPolygon2d>();
        public List<PolyLine2d> EmbeddedPaths = new List<PolyLine2d>();
        public List<PolyLine2d> ClippedPaths = new List<PolyLine2d>();
        public List<GeneralPolygon2d> InputSupportSolids = new List<GeneralPolygon2d>();

        /*
         *  Output geometry, produced by Resolve(). These should not have any intersections.
         *     - "solid" polygons-with-holes
         *     - open paths
         *     - support solids
         */

        public List<GeneralPolygon2d> Solids = new List<GeneralPolygon2d>();
        public List<PolyLine2d> Paths = new List<PolyLine2d>();
        public List<GeneralPolygon2d> SupportSolids = new List<GeneralPolygon2d>();



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
            get { return Solids.Count == 0 && Paths.Count == 0 && SupportSolids.Count == 0; }
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



        public void AddSupportPolygon(GeneralPolygon2d poly)
        {
            if (poly.Outer.IsClockwise)
                poly.Reverse();
            SupportSolids.Add(poly);
        }
        public void AddSupportPolygons(IEnumerable<GeneralPolygon2d> polys)
        {
            foreach (GeneralPolygon2d p in polys)
                AddSupportPolygon(p);
        }


        /// <summary>
        /// Convert assembly of polygons, polylines, etc, into a set of printable solids and paths
        /// </summary>
        public void Resolve()
        {
            // combine solids, process largest-to-smallest
            if (InputSolids.Count > 0) {
                GeneralPolygon2d[] solids = InputSolids.ToArray();
                double[] weights = new double[solids.Length];
                for (int i = 0; i < solids.Length; ++i) {
                    double w = Math.Abs(solids[i].Outer.SignedArea);
                    weights[i] = w;
                }
                Array.Sort(weights, solids); Array.Reverse(solids);


                Solids = new List<GeneralPolygon2d>();
                for ( int k = 0; k < solids.Length; ++k ) {
                    GeneralPolygon2d solid = solids[k];

                    // solid may contain overlapping holes. We need to resolve these before continuing,
                    // otherwise those overlapping regions will be filled by Clipper even/odd rules
                    // [TODO] can we configure clipper to not do this?
                    List<GeneralPolygon2d> resolvedSolid = new List<GeneralPolygon2d>();
                    resolvedSolid.Add(new GeneralPolygon2d(solid.Outer));
                    foreach (Polygon2d hole in solid.Holes) {
                        GeneralPolygon2d holePoly = new GeneralPolygon2d(hole);
                        resolvedSolid = ClipperUtil.PolygonBoolean(resolvedSolid, holePoly, ClipperUtil.BooleanOp.Difference);
                    }

                    // now union in with accumulated solids
                    if (Solids.Count == 0) {
                        Solids.AddRange(resolvedSolid);
                    } else {
                        Solids = ClipperUtil.PolygonBoolean(Solids, resolvedSolid, ClipperUtil.BooleanOp.Union);
                    }
                }
            }

            // subtract thickened embedded paths from solids
            if (EmbeddedPaths.Count > 0 && EmbeddedPathWidth == 0)
                throw new Exception("PlanarSlice.Resolve: must set embedded path width!");
            foreach ( var path in EmbeddedPaths ) {
                Polygon2d thick_path = make_thickened_path(path, EmbeddedPathWidth);
                Solids = ClipperUtil.Difference(Solids, new GeneralPolygon2d(thick_path));
                Paths.Add(path);
            }

            // subtract solids from clipped paths
            foreach ( var path in ClippedPaths ) {
                List<PolyLine2d> clipped = ClipperUtil.ClipAgainstPolygon(Solids, path);
                foreach ( var cp in clipped)
                    Paths.Add(cp);
            }


            // combine support solids, while also subtracting print solids and thickened paths
            if ( InputSupportSolids.Count > 0 ) {

                // make assembly of path solids
                // [TODO] do we need to boolean these?
                List<GeneralPolygon2d> path_solids = null;
                if ( Paths.Count > 0 ) {
                    path_solids = new List<GeneralPolygon2d>();
                    foreach (var path in Paths)
                        path_solids.Add( new GeneralPolygon2d(make_thickened_path(path, EmbeddedPathWidth)) );
                }

                foreach ( var solid in InputSupportSolids) {
                    // [RMS] same as above, we explicitly subtract holes to resolve overlaps
                    List<GeneralPolygon2d> resolved = new List<GeneralPolygon2d>();
                    resolved.Add(new GeneralPolygon2d(solid.Outer));
                    foreach (Polygon2d hole in solid.Holes) {
                        GeneralPolygon2d holePoly = new GeneralPolygon2d(hole);
                        resolved = ClipperUtil.PolygonBoolean(resolved, holePoly, ClipperUtil.BooleanOp.Difference);
                    }

                    // now subtract print solids
                    resolved = ClipperUtil.PolygonBoolean(resolved, Solids, ClipperUtil.BooleanOp.Difference);

                    // now subtract paths
                    if ( path_solids != null )
                        resolved = ClipperUtil.PolygonBoolean(resolved, path_solids, ClipperUtil.BooleanOp.Difference);

                    // now union in with accumulated support solids
                    if (SupportSolids.Count == 0) {
                        SupportSolids.AddRange(resolved);
                    } else {
                        SupportSolids = ClipperUtil.PolygonBoolean(Solids, resolved, ClipperUtil.BooleanOp.Union);
                    }
                }
            }


        }



        protected Polygon2d make_thickened_path(PolyLine2d path, double width)
        {
            PolyLine2d pos = new PolyLine2d(path), neg = new PolyLine2d(path);
            pos.VertexOffset(width / 2);
            neg.VertexOffset(-width / 2); neg.Reverse();
            pos.AppendVertices(neg);
            Polygon2d poly = new Polygon2d(pos.Vertices);
            if (poly.IsClockwise)
                poly.Reverse();
            return poly;
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
                foreach (GeneralPolygon2d poly in InputSupportSolids)
                    box.Contain(poly.Outer.Bounds);
                return box;
			}
		}





        public void Store(BinaryWriter writer)
        {
            writer.Write(Z);

            writer.Write(InputSolids.Count);
            for (int k = 0; k < InputSolids.Count; ++k)
                gSerialization.Store(InputSolids[k], writer);
            writer.Write(EmbeddedPaths.Count);
            for (int k = 0; k < EmbeddedPaths.Count; ++k)
                gSerialization.Store(EmbeddedPaths[k], writer);
            writer.Write(ClippedPaths.Count);
            for (int k = 0; k < ClippedPaths.Count; ++k)
                gSerialization.Store(ClippedPaths[k], writer);
            writer.Write(InputSupportSolids.Count);
            for (int k = 0; k < InputSupportSolids.Count; ++k)
                gSerialization.Store(InputSupportSolids[k], writer);


            writer.Write(Solids.Count);
            for (int k = 0; k < Solids.Count; ++k)
                gSerialization.Store(Solids[k], writer);
            writer.Write(Paths.Count);
            for (int k = 0; k < Paths.Count; ++k)
                gSerialization.Store(Paths[k], writer);
            writer.Write(SupportSolids.Count);
            for (int k = 0; k < SupportSolids.Count; ++k)
                gSerialization.Store(SupportSolids[k], writer);
        }


        public void Restore(BinaryReader reader)
        {
            Z = reader.ReadDouble();

            int nInputSolids = reader.ReadInt32();
            InputSolids = new List<GeneralPolygon2d>();
            for (int k = 0; k < nInputSolids; ++k)
                gSerialization.Restore(InputSolids[k], reader);
            int nEmbeddedPaths = reader.ReadInt32();
            EmbeddedPaths = new List<PolyLine2d>();
            for (int k = 0; k < nEmbeddedPaths; ++k)
                gSerialization.Restore(EmbeddedPaths[k], reader);
            int nClippedPaths = reader.ReadInt32();
            ClippedPaths = new List<PolyLine2d>();
            for (int k = 0; k < nClippedPaths; ++k)
                gSerialization.Restore(ClippedPaths[k], reader);
            int nInputSupportSolids = reader.ReadInt32();
            InputSupportSolids = new List<GeneralPolygon2d>();
            for (int k = 0; k < nInputSupportSolids; ++k)
                gSerialization.Restore(InputSupportSolids[k], reader);

            int nSolids = reader.ReadInt32();
            Solids = new List<GeneralPolygon2d>();
            for (int k = 0; k < nSolids; ++k)
                gSerialization.Restore(Solids[k], reader);
            int nPaths = reader.ReadInt32();
            Paths = new List<PolyLine2d>();
            for (int k = 0; k < nPaths; ++k)
                gSerialization.Restore(Paths[k], reader);
            int nSupportSolids = reader.ReadInt32();
            SupportSolids = new List<GeneralPolygon2d>();
            for (int k = 0; k < nSupportSolids; ++k)
                gSerialization.Restore(SupportSolids[k], reader);
        }



    }
}

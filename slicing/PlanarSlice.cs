﻿﻿using System;
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
        public static double MIN_AREA = 0.001;      // polygons/holes smaller than this are discarded

        public int LayerIndex = 0;
		public double Z = 0;
        public Interval1d LayerZSpan;

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
        public List<GeneralPolygon2d> InputCavities = new List<GeneralPolygon2d>();
        public List<GeneralPolygon2d> InputSupportSolids = new List<GeneralPolygon2d>();
        public List<GeneralPolygon2d> InputCropRegions = new List<GeneralPolygon2d>();

        public Dictionary<GeneralPolygon2d, double> Clearances = new Dictionary<GeneralPolygon2d, double>();
        public Dictionary<GeneralPolygon2d, double> Offsets = new Dictionary<GeneralPolygon2d, double>();

        public Dictionary<GeneralPolygon2d, double> Cavity_Clearances = new Dictionary<GeneralPolygon2d, double>();
        public Dictionary<GeneralPolygon2d, double> Cavity_Offsets = new Dictionary<GeneralPolygon2d, double>();

        protected Dictionary<GeneralPolygon2d, List<GeneralPolygon2d>> Thickened;

        public List<Vector2d> InputSupportPoints = new List<Vector2d>();

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

        public void AddCavityPolygon(GeneralPolygon2d poly)
        {
            if (poly.Outer.IsClockwise)
                poly.Reverse();
            InputCavities.Add(poly);
        }

        public void AddCavityPolygons(IEnumerable<GeneralPolygon2d> polys)
        {
            foreach (GeneralPolygon2d p in polys)
                AddCavityPolygon(p);
        }

        public void AddCropRegion(GeneralPolygon2d poly)
        {
            if (poly.Outer.IsClockwise)
                poly.Reverse();
            InputCropRegions.Add(poly);
        }
        public void AddCropRegions(IEnumerable<GeneralPolygon2d> polys) {
            foreach (GeneralPolygon2d p in polys)
                AddCropRegion(p);
        }

        /// <summary>
        /// Convert assembly of polygons, polylines, etc, into a set of printable solids and paths
        /// </summary>
        public virtual void Resolve()
        {
            // combine solids, process largest-to-smallest
            if (InputSolids.Count > 0) {
                GeneralPolygon2d[] solids = InputSolids.ToArray();

                solids = process_input_polys_before_sort(solids);

                // sort by decreasing weight
                double[] weights = new double[solids.Length];
                for (int i = 0; i < solids.Length; ++i) 
                    weights[i] = sorting_weight(solids[i]);
                Array.Sort(weights, solids); Array.Reverse(solids);

                solids = process_input_polys_after_sort(solids);

                Solids = new List<GeneralPolygon2d>();
                for ( int k = 0; k < solids.Length; ++k ) {

                    // convert this polygon into the solid we want to use
                    List<GeneralPolygon2d> resolvedSolid = make_solid(solids[k], false);

                    // now union in with accumulated solids
                    if (Solids.Count == 0) {
                        Solids.AddRange(resolvedSolid);
                    } else {
                        Solids = combine_solids(Solids, resolvedSolid);
                    }
                }
            }

            // subtract input cavities
            foreach (var cavity in InputCavities) {
                Solids = remove_cavity(Solids, cavity);
            }

            // subtract thickened embedded paths from solids
            if (EmbeddedPaths.Count > 0 && EmbeddedPathWidth == 0)
                throw new Exception("PlanarSlice.Resolve: must set embedded path width!");
            foreach ( var path in EmbeddedPaths ) {
                Polygon2d thick_path = make_thickened_path(path, EmbeddedPathWidth);
                Solids = ClipperUtil.Difference(Solids, new GeneralPolygon2d(thick_path), MIN_AREA);
                Paths.Add(path);
            }

            // cleanup
            Solids = filter_solids(Solids);

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

                    // convert this polygon into the solid we want to use
                    List<GeneralPolygon2d> resolved = make_solid(solid, true);

                    // now subtract print solids
                    resolved = ClipperUtil.PolygonBoolean(resolved, Solids, ClipperUtil.BooleanOp.Difference, MIN_AREA);

                    // now subtract paths
                    if ( path_solids != null )
                        resolved = ClipperUtil.PolygonBoolean(resolved, path_solids, ClipperUtil.BooleanOp.Difference, MIN_AREA);

                    // now union in with accumulated support solids
                    if (SupportSolids.Count == 0) {
                        SupportSolids.AddRange(resolved);
                    } else {
                        SupportSolids = ClipperUtil.PolygonBoolean(SupportSolids, resolved, ClipperUtil.BooleanOp.Union, MIN_AREA);
                    }
                }

                SupportSolids = filter_solids(SupportSolids);
            }


            // apply crop regions
            if ( InputCropRegions.Count > 0 ) {
                // combine crop regions
                var CropRegions = make_solid(InputCropRegions[0], false);
                for (int k = 1; k < InputCropRegions.Count; ++k)
                    CropRegions = combine_solids(CropRegions, make_solid(InputCropRegions[k], false));

                Solids = ClipperUtil.Intersection(CropRegions, Solids, MIN_AREA);
                Solids = filter_solids(Solids);
                List<PolyLine2d> cropped_paths = new List<PolyLine2d>();
                foreach ( var path in Paths ) 
                    cropped_paths.AddRange(ClipperUtil.ClipAgainstPolygon(CropRegions, path, true));
                // TODO: filter paths

                SupportSolids = ClipperUtil.Intersection(CropRegions, SupportSolids, MIN_AREA);
                SupportSolids = filter_solids(SupportSolids);
            }


        }

        /*
         *  functions for subclasses to override to customize behavior
         */

        protected virtual GeneralPolygon2d[] process_input_polys_before_sort(GeneralPolygon2d[] polys) {
            if (Offsets.Count == 0)
                return polys;
            List<GeneralPolygon2d> newPolys = new List<GeneralPolygon2d>();
            bool modified = false;
            foreach (var poly in polys)
            {
                double offset;
                if (Offsets.TryGetValue(poly, out offset) && Math.Abs(offset) > MathUtil.ZeroTolerancef)
                {
                    List<GeneralPolygon2d> offsetPolys = ClipperUtil.MiterOffset(poly, offset);
                    foreach (var newpoly in offsetPolys)
                    {
                        transfer_tags(poly, newpoly);
                        newPolys.Add(newpoly);
                    }
                    modified = true;
                }
                else
                    newPolys.Add(poly);
            }
            if (modified == false)
                return polys;
            return newPolys.ToArray();
        }

        protected virtual GeneralPolygon2d[] process_input_polys_after_sort(GeneralPolygon2d[] solids) {
            // construct thickened solids
            Thickened = new Dictionary<GeneralPolygon2d, List<GeneralPolygon2d>>();
            for (int k = 0; k < solids.Length; ++k)
            {
                double clearance;
                if (Clearances.TryGetValue(solids[k], out clearance) && clearance > 0)
                    Thickened.Add(solids[k], ClipperUtil.MiterOffset(solids[k], clearance));
            }

            return solids;
        }

        protected virtual double sorting_weight(GeneralPolygon2d poly) {
            return poly.Outer.Area;
        }

        protected virtual List<GeneralPolygon2d> make_solid(GeneralPolygon2d poly, bool bIsSupportSolid)
        {
            // always do a self-union of outer polygon. This allows Clipper to clean up many
            // problems in input polygons, eg self-intersections and other junk
            GeneralPolygon2d gouter = new GeneralPolygon2d(poly.Outer);
            List<GeneralPolygon2d> resolvedSolid =
                ClipperUtil.Union(gouter, gouter, MIN_AREA);

            // solid may contain overlapping holes. We need to resolve these before continuing,
            // otherwise those overlapping regions will be filled by Clipper even/odd rules
            foreach (Polygon2d hole in poly.Holes) {
                GeneralPolygon2d holePoly = new GeneralPolygon2d(hole);
                resolvedSolid = ClipperUtil.PolygonBoolean(resolvedSolid, holePoly, ClipperUtil.BooleanOp.Difference, MIN_AREA);
            }

            if (bIsSupportSolid == false && Thickened != null)
            {
                // Subtract away any clearance solids
                foreach (var pair in Thickened)
                {
                    if (pair.Key != poly)
                        resolvedSolid = ClipperUtil.Difference(resolvedSolid, pair.Value);
                }

            }
            return filter_solids(resolvedSolid);
        }

        protected virtual List<GeneralPolygon2d> combine_solids(List<GeneralPolygon2d> all_solids, List<GeneralPolygon2d> new_solids)
        {
            return ClipperUtil.PolygonBoolean(all_solids, new_solids, ClipperUtil.BooleanOp.Union, MIN_AREA);
        }

        protected virtual List<GeneralPolygon2d> remove_cavity(List<GeneralPolygon2d> solids, GeneralPolygon2d cavity)
        {
            double offset = 0;
            if (Cavity_Clearances.ContainsKey(cavity))
            {
                offset = Cavity_Clearances[cavity];
            }
            if (Cavity_Offsets.ContainsKey(cavity))
            {
                offset += Cavity_Offsets[cavity];
            }
            if (Math.Abs(offset) > 0.0001)
            {
                var offset_cavities = ClipperUtil.MiterOffset(cavity, offset, MIN_AREA);
                return ClipperUtil.Difference(solids, offset_cavities, MIN_AREA);
            }
            else
            {
                return ClipperUtil.Difference(solids, cavity, MIN_AREA);
            }
        }

        protected virtual List<GeneralPolygon2d> filter_solids(List<GeneralPolygon2d> solids)
        {
            if (MIN_AREA > 0) {
                return CurveUtils2.FilterDegenerate(solids, MIN_AREA);
            } else
                return solids;
        }

        /// <summary>
        /// Use during Resolve() processing to transfer tags/metadata to child polygons
        /// created by processing operations.
        /// </summary>
        protected virtual void transfer_tags(GeneralPolygon2d oldPoly, GeneralPolygon2d newPoly)
        {
            if (Tags.Has(oldPoly)) {
                int t = Tags.Get(oldPoly);
                Tags.Add(newPoly, t);
            }

            double clearance;
            if (Clearances.TryGetValue(oldPoly, out clearance))
                Clearances[newPoly] = clearance;

            double offset;
            if (Offsets.TryGetValue(oldPoly, out offset))
                Offsets[newPoly] = offset;
        }

        protected virtual Polygon2d make_thickened_path(PolyLine2d path, double width)
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

                // [TODO] should crop against crop regions...

                return box;
			}
		}

		/// <summary>
		/// Returns the unsigned minimum distance to the solid/path polylines.
		/// Must call BuildSpatialCaches() first, then it is safe to call
		/// this function from multiple threads.
		/// </summary>
		public double DistanceSquared(Vector2d pt, double max_dist = double.MaxValue, bool solids = true, bool paths = true)
		{
			if (spatial_caches_available == false)
				throw new Exception("PlanarSlice.DistanceSquared: call BiuldSpatialCaches first!");
			
			double dist_sqr = double.MaxValue;
			if (max_dist != double.MaxValue)
				max_dist = max_dist * max_dist;

			int NS = Solids.Count;
			for (int i = 0; i < NS; ++i) {
				double d = solid_bounds[i].Distance(pt);
				if (d * d > dist_sqr)
					continue;
				int iHole, iSeg; double segT;
				d = Solids[i].DistanceSquared(pt, out iHole, out iSeg, out segT);
				if (d < dist_sqr)
					dist_sqr = d;
			}
			int NP = Paths.Count;
			for (int i = 0; i < NP; ++i) {
				double d = path_bounds[i].Distance(pt);
				if (d * d > dist_sqr)
					continue;				
				d = Paths[i].DistanceSquared(pt);
				if (d < dist_sqr)
					dist_sqr = d;
			}
			return dist_sqr;
		}

		/// <summary>
		/// Precompute spatial caching information. This is not thread-safe.
		/// (Currently just list of bboxes for each solid/path.)
		/// </summary>
		public void BuildSpatialCaches()
		{
			int NS = Solids.Count;
			solid_bounds = new AxisAlignedBox2d[NS];
			for (int i = 0; i < NS; ++i) {
				solid_bounds[i] = Solids[i].Bounds;
			}

			int NP = Paths.Count;
			path_bounds = new AxisAlignedBox2d[NP];
			for (int i = 0; i < NP; ++i) {
				path_bounds[i] = Paths[i].Bounds;
			}

			spatial_caches_available = true;
		}
		AxisAlignedBox2d[] solid_bounds;
		AxisAlignedBox2d[] path_bounds;
		bool spatial_caches_available = false;

        public void Store(BinaryWriter writer)
        {
            writer.Write(LayerIndex);
            writer.Write(Z);
            writer.Write(LayerZSpan.a);
            writer.Write(LayerZSpan.b);

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
            writer.Write(InputCavities.Count);
            for (int k = 0; k < InputCavities.Count; ++k)
                gSerialization.Store(InputCavities[k], writer);
            writer.Write(InputCropRegions.Count);
            for (int k = 0; k < InputCropRegions.Count; ++k)
                gSerialization.Store(InputCropRegions[k], writer);

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
            LayerIndex = reader.ReadInt32();
            Z = reader.ReadDouble();
            LayerZSpan.a = reader.ReadDouble();
            LayerZSpan.b = reader.ReadDouble();

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
            int nInputCavities = reader.ReadInt32();
            InputCavities = new List<GeneralPolygon2d>();
            for (int k = 0; k < nInputCavities; ++k)
                gSerialization.Restore(InputCavities[k], reader);
            int nInputCropRegions = reader.ReadInt32();
            InputCropRegions = new List<GeneralPolygon2d>();
            for (int k = 0; k < nInputCropRegions; ++k)
                gSerialization.Restore(InputCropRegions[k], reader);

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

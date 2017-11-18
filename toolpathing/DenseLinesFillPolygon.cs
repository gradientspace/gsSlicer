using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	public class DenseLinesFillPolygon : IPathsFillPolygon
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
		public List<FillPaths2d> Paths { get; set; }
        public List<FillPaths2d> GetFillPaths() { return Paths; }

        // [RMS] only using this for hit-testing to make sure no connectors cross polygon border...
        // [TODO] replace with GeneralPolygon2dBoxTree (currently does not have intersection test!)
        SegmentSet2d BoundaryPolygonCache;

		public DenseLinesFillPolygon(GeneralPolygon2d poly)
		{
			Polygon = poly;
			Paths = new List<FillPaths2d>();
		}


		public bool Compute()
		{
			if ( InsetFromInputPolygon ) {
				BoundaryPolygonCache = new SegmentSet2d(Polygon);
				List<GeneralPolygon2d> current = ClipperUtil.ComputeOffsetPolygon(Polygon, -ToolWidth / 2, true);
				foreach (GeneralPolygon2d poly in current) {
					Paths.Add(ComputeFillPaths(poly));
				}

			} else {
				List<GeneralPolygon2d> boundary = ClipperUtil.ComputeOffsetPolygon(Polygon, ToolWidth / 2, true);
				BoundaryPolygonCache = new SegmentSet2d(boundary);
				Paths.Add(ComputeFillPaths(Polygon));
			}


			return true;
		}




		protected FillPaths2d ComputeFillPaths(GeneralPolygon2d poly) 
		{
            FillPaths2d paths = new FillPaths2d();

            List<Segment2d>[] StepSpans = ComputeSegments(poly);
            if ( StepSpans == null ) {
                return paths;
            }
			int N = StepSpans.Length;

			double hard_max_dist = 5 * ToolWidth;

			// [TODO] need a pathfinder here, that can chain segments efficiently

			bool is_dense = Math.Abs(PathSpacing - ToolWidth) < (ToolWidth * 0.2f);
			PathTypeFlags pathType = is_dense ? PathTypeFlags.SolidInfill : PathTypeFlags.SparseInfill;
			                             


			// (for now just do dumb things?)

			FillPolyline2d cur = new FillPolyline2d() { TypeFlags = pathType };
			Vector2d prev = Vector2d.Zero;

			int iStart = 0;
			int iCur = iStart;
			// [TODO] pick 'best' starting span?


			// make repeated sweeps over spans until we used them all
			// [TODO] support reversing direction when we hit end? less travel.
			// [TODO] support branching lookahead? in some situations it coule be
			//    better to 'go back' around an island, then to continue in the
			//    direction we are moving.

			bool all_spans_used = false;
			while (all_spans_used == false) {
				all_spans_used = true;

				for (int i = iCur; i < N; ++i) {
					List<Segment2d> spans = StepSpans[i];
					int M = spans.Count;

					// if we hit no-spans case, terminate current path and start a new one
					if (M == 0) {
						if (cur != null && cur.VertexCount > 0) {
							paths.Curves.Add(cur);
							cur = new FillPolyline2d() { TypeFlags = pathType };
						}
						continue;
					}
					all_spans_used = false;

					// find closest point to our previous point
					bool reverse = false;
					int j = find_nearest_span_endpoint(spans, prev, out reverse);
					Vector2d P0 = spans[j].Endpoint(reverse ? 1 : 0);
					Vector2d P1 = spans[j].Endpoint(reverse ? 0 : 1);


					// if we are not in a path, start one - easy!
					if (cur.VertexCount == 0) {
						cur.AppendVertex(P0);
						cur.AppendVertex(P1);
						prev = cur.End;
						spans.RemoveAt(j);
						continue;
					}

					// distance to start of closest available segment
					double next_dist = prev.Distance(P0);

                    // if too far, we have to check for intersections, etc
                    if (next_dist > ToolWidth * 2) {
						bool terminate = false;

                        Segment2d join_seg = new Segment2d(prev, P0);

                        int hit_i = 0;
						if ( BoundaryPolygonCache.FindAnyIntersection(join_seg, out hit_i) != null )
							terminate = true;

						if (terminate == false && next_dist > hard_max_dist)
							terminate = true;

                        // NO! P0 and P1 are endpoints!!
                        //Segment2d first_path_seg = new Segment2d(P0, P1);
                        //double angle = Vector2d.AngleD(join_seg.Direction, first_path_seg.Direction);
                        //if (angle < 45)
                        //    terminate = true;

                        // [TODO] an alternative to terminating is to reverse
                        //   existing path. however this may have its own
                        //   problems...

                        if (terminate) {
							// too far! end this path and start a new one
							paths.Curves.Add(cur);
							cur = new FillPolyline2d() { TypeFlags = pathType };
						}
					}

					if (cur.VertexCount > 0)
						cur.AppendVertex(P0, PathUtil.ConnectorVFlag);
					else
						cur.AppendVertex(P0);

                    cur.AppendVertex(P1);
					prev = cur.End;
					spans.RemoveAt(j);
				}
			}

			// if we still have an open path, end it
			if ( cur.VertexCount > 0 )
				paths.Curves.Add(cur);

			// chain open paths, etc
			paths.OptimizeCurves(2*PathSpacing, (seg) => {
				int hit_i = 0;
				if (BoundaryPolygonCache.FindAnyIntersection(seg, out hit_i) != null)
					return false;
				return true;
			});


			return paths;
		}



		// finds segment endpoint in spans closest to input point
		int find_nearest_span_endpoint(List<Segment2d> spans, Vector2d prev, out bool reverse)
		{
			reverse = false;
			int N = spans.Count;
			int iNearest = -1;
			double dNearest = double.MaxValue;
			for (int i = 0; i < N; ++i) {
				double d0 = prev.DistanceSquared(spans[i].P0);
				double d1 = prev.DistanceSquared(spans[i].P1);
				double min = Math.Min(d0, d1);
				if ( min < dNearest ) {
					dNearest = min;
					iNearest = i;
					if (d1 < d0)
						reverse = true;
				}
			}
			return iNearest;
		}







        protected List<Segment2d>[] ComputeSegments(GeneralPolygon2d poly)
        {
            double angleRad = AngleDeg * MathUtil.Deg2Rad;
            Vector2d dir = new Vector2d(Math.Cos(angleRad), Math.Sin(angleRad));

            // compute projection span along axis
            Vector2d axis = dir.Perp;
            Interval1d axisInterval = Interval1d.Empty;
            Interval1d dirInterval = Interval1d.Empty;
            foreach (Vector2d v in poly.Outer.Vertices) {
                dirInterval.Contain(v.Dot(dir));
                axisInterval.Contain(v.Dot(axis));
            }
            // [TODO] also check holes? or assume they are contained?

            dirInterval.a -= 10 * ToolWidth;
            dirInterval.b += 10 * ToolWidth;
            double extent = dirInterval.Length;

            axisInterval.a += ToolWidth * 0.1 + PathShift;
            axisInterval.b -= ToolWidth * 0.1;
            if (axisInterval.b < axisInterval.a)
                return null;     // [RMS] is this right? I guess so. interval is too small to fill?

            Vector2d startCorner = axisInterval.a * axis + dirInterval.a * dir;
            double range = axisInterval.Length;
            int N = (int)(range / PathSpacing);

            DGraph2 graph = new DGraph2();
            graph.AppendPolygon(poly);
            GraphSplitter2d splitter = new GraphSplitter2d(graph);
            splitter.InsideTestF = poly.Contains;

            for (int ti = 0; ti <= N; ++ti) {
                double t = (double)ti / (double)N;
                Vector2d o = startCorner + (t * range) * axis;
                Line2d ray = new Line2d(o, dir);

                splitter.InsertLine(ray, ti);
            }

            List<Segment2d>[] PerRaySpans = new List<Segment2d>[N+1];
            for (int ti = 0; ti <= N; ++ti)
                PerRaySpans[ti] = new List<Segment2d>();

            foreach ( int eid in graph.EdgeIndices() ) {
                int gid = graph.GetEdgeGroup(eid);
                if (gid >= 0)
                    PerRaySpans[gid].Add(graph.GetEdgeSegment(eid));
            }

            return PerRaySpans;
        }




                


	}
}

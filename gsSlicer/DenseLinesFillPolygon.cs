using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	public class DenseLinesFillPolygon : IFillPolygon
	{
		// polygon to fill
		public GeneralPolygon2d Polygon { get; set; }

		// parameters
		public double ToolWidth = 0.4;
		public double PathSpacing = 0.4;
		public double AngleDeg = 45.0;

		// [RMS] improve this...
		public double WeirdFudgeFactor = 1.5f;

		// if true, we inset half of tool-width from Polygon
		public bool InsetFromInputPolygon = true;

		// fill paths
		public List<FillPaths2d> Paths { get; set; }


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




		protected FillPaths2d ComputeFillPaths(GeneralPolygon2d poly, SegmentSet2d polyCache) 
		{
			List<List<Segment2d>> StepSpans = ComputeSegments(poly, polyCache);
			int N = StepSpans.Count;

			// [TODO] need a pathfinder here, that can chain segments efficiently

			// (for now just do dumb things?)

			FillPaths2d paths = new FillPaths2d();
			PolyLine2d cur = new PolyLine2d();
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
							cur = new PolyLine2d();
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
					if (next_dist > PathSpacing * WeirdFudgeFactor) {
						bool terminate = false;

						Segment2d seg = new Segment2d(prev, P0);
						int hit_i = 0;
						if ( BoundaryPolygonCache.FindAnyIntersection(seg, out hit_i) != null )
							terminate = true;

						// [TODO] an alternative to terminating is to reverse
						//   existing path. however this may have its own
						//   problems...

						if (terminate) {
							// too far! end this path and start a new one
							paths.Curves.Add(cur);
							cur = new PolyLine2d();
						}
					}

					cur.AppendVertex(P0);
					cur.AppendVertex(P1);
					prev = cur.End;
					spans.RemoveAt(j);
				}
			}

			// if we still have an open path, end it
			if ( cur.VertexCount > 0 )
				paths.Curves.Add(cur);
			
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



		protected List<List<Segment2d>> ComputeSegments(GeneralPolygon2d poly, SegmentSet2d polyCache) {

			double angleRad = AngleDeg * MathUtil.Deg2Rad;
			Vector2d dir = new Vector2d(Math.Cos(angleRad), Math.Sin(angleRad));

			// compute projection span along axis
			Vector2d axis = dir.Perp;
			Interval1d axisInterval = Interval1d.Empty;
			Interval1d dirInterval = Interval1d.Empty;
			foreach ( Vector2d v in poly.Outer ) {
				dirInterval.Contain(v.Dot(dir));
				axisInterval.Contain(v.Dot(axis));
			}
			// [TODO] also check holes? or assume they are contained?

			dirInterval.a -= 10 * ToolWidth;
			dirInterval.b += 10 * ToolWidth;
			double extent = dirInterval.Length;

			axisInterval.a += ToolWidth * 0.1;
			axisInterval.b -= ToolWidth * 0.1;
			if (axisInterval.b < axisInterval.a)
				throw new Exception("DenseLinesFillPolygon: interval is empty - what to do in this case??");

			Vector2d startCorner = axisInterval.a * axis + dirInterval.a * dir;
			double range = axisInterval.Length;
			int N = (int)(range / PathSpacing);

			List<List<Segment2d>> PerRaySpans = new List<List<Segment2d>>();
			for (int ti = 0; ti <= N; ++ti ) {
				double t = (double)ti / (double)N;
				Vector2d o = startCorner + (t * range) * axis;
				Segment2d ray = new Segment2d(o, o + extent * dir);

				List<Segment2d> spans = compute_polygon_ray_spans(ray, startCorner, axis, t, polyCache);
				PerRaySpans.Add(spans);
			}

			return PerRaySpans;
		}



		// yikes not robust at all!!
		protected List<Segment2d> compute_polygon_ray_spans(Segment2d ray, Vector2d axis_origin, Vector2d axis, double axisT, SegmentSet2d segments) 
		{

			List<double> hits = new List<double>();     // todo reusable buffer
			segments.FindAllIntersections(ray, hits, null, null, true);
			hits.Sort();

			if (hits.Count % 2 != 0)
				throw new Exception("DenseLineFill.ComputeAllSpans: have not handled hard cases...");

			List<Segment2d> spans = new List<Segment2d>();
			for (int i = 0; i < hits.Count / 2; ++i ) {
				Vector2d p0 = ray.PointAt(hits[2 * i]);
				Vector2d p1 = ray.PointAt(hits[2 * i + 1]);
				spans.Add(new Segment2d(p0, p1));
			}

			return spans;
		}






	}
}

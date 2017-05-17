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


		public DenseLinesFillPolygon(GeneralPolygon2d poly)
		{
			Polygon = poly;
			Paths = new List<FillPaths2d>();
		}


		public bool Compute()
		{
			// first shell is either polygon, or inset from that polygon
			List<GeneralPolygon2d> current = (InsetFromInputPolygon) ?
				ClipperUtil.ComputeOffsetPolygon(Polygon, -ToolWidth / 2, true) :
			   	new List<GeneralPolygon2d>() { Polygon };

			foreach (GeneralPolygon2d poly in current)
				Paths.Add(ComputeFillPaths(poly));

			return true;
		}




		protected FillPaths2d ComputeFillPaths(GeneralPolygon2d poly) 
		{
			List<List<Segment2d>> StepSpans = ComputeSegments(poly);

			// [TODO] need a pathfinder here, that can chain segments efficiently

			// (for now just do dumb things?)

			FillPaths2d paths = new FillPaths2d();
			PolyLine2d cur = new PolyLine2d();
			Vector2d prev = Vector2d.Zero;

			int N = StepSpans.Count;
			for (int i = 0; i < N; ++i ) {
				int M = StepSpans[i].Count;
				if (M != 1)
					throw new Exception("DenseLineFill.ComputeFillPaths: only handling M = 1...");

				Segment2d seg = StepSpans[i][0];

				// if we are not in a path, start one - easy!
				if ( cur.VertexCount == 0 ) {
					cur.AppendVertex(seg.P0);
					cur.AppendVertex(seg.P1);
					prev = seg.P1;
					continue;
				}

				double d0 = prev.Distance(seg.P0);
				double d1 = prev.Distance(seg.P1);

				// if closest starting point is too far away to connect to,
				// close current path and start a new one
				if ( Math.Min(d0,d1) > PathSpacing*WeirdFudgeFactor) {
					// too far! end this path and start a new one
					paths.Curves.Add(cur);
					cur = new PolyLine2d();
				}

				// start segment at point closer to previous point
				if ( d0 < d1 ) {
					cur.AppendVertex(seg.P0);
					cur.AppendVertex(seg.P1);
				} else {
					cur.AppendVertex(seg.P1);
					cur.AppendVertex(seg.P0);					
				}
				prev = cur.End;
			}

			if ( cur.VertexCount > 0 )
				paths.Curves.Add(cur);
			return paths;
		}




		protected List<List<Segment2d>> ComputeSegments(GeneralPolygon2d poly) {

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

			// [TODO] should be using a spatial DS here, no? or at least can sort along axis!!
			// [TODO] maybe should keep separate, and use bboxes of holes??
			List<Segment2d> segs = new List<Segment2d>(poly.Outer.SegmentItr());
			foreach (var hole in poly.Holes)
				segs.AddRange(hole.SegmentItr());


			List<List<Segment2d>> PerRaySpans = new List<List<Segment2d>>();
			for (int ti = 0; ti <= N; ++ti ) {
				double t = (double)ti / (double)N;
				Vector2d o = startCorner + (t * range) * axis;
				Segment2d ray = new Segment2d(o, o + extent * dir);

				List<Segment2d> spans = ComputeAllRaySpans(ray, startCorner, axis, t, segs);
				if (spans.Count != 1)
					throw new Exception("DenseLinesFill.ComputeSegments: have not handled hard cases!!");
				PerRaySpans.Add(spans);
			}

			return PerRaySpans;
		}



		// yikes not robust at all!!
		protected List<Segment2d> ComputeAllRaySpans(Segment2d ray, Vector2d axis_origin, Vector2d axis, double axisT, List<Segment2d> loopSegments) 
		{
			List<double> hits = new List<double>();

			int N = loopSegments.Count;
			for (int i = 0; i < N; ++i ) {

				// why doesn't this work??
				//Interval1d interval = Interval1d.Unsorted(
					//(loopSegments[i].P0 - axis_origin).Dot(axis),
					//(loopSegments[i].P1 - axis_origin).Dot(axis) );
				//if (!interval.Contains(axisT))
					//continue;

				IntrSegment2Segment2 intr = new IntrSegment2Segment2(ray, loopSegments[i]);
				if (intr.Find()) {
					// skip non-simple intersections, they are "on" a segment and
					// we don't need it (right?)

					// NO SUPER WRONG AAAHHHH span might be needed to terminate ray!!
					// Maybe try slightly perturbing in this case??

					if ( intr.IsSimpleIntersection )
						hits.Add(intr.Parameter0);
				}
			}

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

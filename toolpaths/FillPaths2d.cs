using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using g3;

namespace gs
{
	public class FillPolyline2d : PolyLine2d
	{
		List<Index3i> flags;
		bool has_flags = false;

		void alloc_flags() {
			if (flags == null) {
				flags = new List<Index3i>();
				for (int i = 0; i < vertices.Count; ++i)
					flags.Add(Index3i.Zero);
			}
		}

		public override void AppendVertex(Vector2d v) {
			base.AppendVertex(v);
			if (flags != null)
				flags.Add(Index3i.Zero);
		}
		public override void AppendVertices(IEnumerable<Vector2d> v) {
			base.AppendVertices(v);
			if (flags != null) {
				foreach (var x in v)
					flags.Add(Index3i.Zero);
			}
		}

		public override void Reverse() {
			base.Reverse();
			if (flags != null)
				flags.Reverse();
		}
		public override void Simplify(double clusterTol = 0.0001,
							  		  double lineDeviationTol = 0.01,
		                              bool bSimplifyStraightLines = true) {
			throw new Exception("not supported yet...");
		}


		public void AppendVertex(Vector2d v, Index3i flag) {
			alloc_flags();			
			base.AppendVertex(v);
			flags.Add(flag);
			has_flags = true;
		}
		public void AppendVertices(IEnumerable<Vector2d> v, IEnumerable<Index3i> f) {
			alloc_flags();			
			base.AppendVertices(v);
			flags.AddRange(f);
			has_flags = true;
		}

		public Index3i GetFlag(int i) { return (flags == null) ? Index3i.Zero: flags[i]; }
		public void SetFlag(int i, Index3i flag) { alloc_flags(); flags[i] = flag; }

		public bool HasFlags { 
			get { return flags != null && has_flags; } 
		}
		public ReadOnlyCollection<Index3i> Flags() { return flags.AsReadOnly(); }
	}




	public class FillPaths2d
	{
		public List<Polygon2d> Loops = new List<Polygon2d>();
		public List<FillPolyline2d> Curves = new List<FillPolyline2d>();

		public FillPaths2d()
		{
		}


		public void Append(GeneralPolygon2d poly) {
			Loops.Add(new Polygon2d(poly.Outer));
			foreach (var h in poly.Holes)
				Loops.Add(new Polygon2d(h));
		}

		public void Append(List<GeneralPolygon2d> polys) {
			foreach (var p in polys)
				Append(p);
		}






		public void OptimizeCurves(double max_dist, Func<Segment2d, bool> ValidateF) {
			int[] which = new int[4];
			double[] dists = new double[4];
			for (int ci = 0; ci < Curves.Count; ++ci ) {
				FillPolyline2d l0 = Curves[ci];

				// find closest viable connection
				int iClosest = -1;
				int iClosestCase = -1;
				for (int cj = ci + 1; cj < Curves.Count; ++cj) {
					FillPolyline2d l1 = Curves[cj];
					dists[0] = l0.Start.Distance(l1.Start);  which[0] = 0;
					dists[1] = l0.Start.Distance(l1.End);  which[1] = 1;
					dists[2] = l0.End.Distance(l1.Start);  which[2] = 2;
					dists[3] = l0.End.Distance(l1.End);  which[3] = 3;
					Array.Sort(dists, which);

					for (int k = 0; k < 4 && iClosest != cj; ++k) {
						if (dists[k] > max_dist)
							continue;
						Segment2d connector = get_case(l0, l1, which[k]);
						if (ValidateF(connector) == false)
							continue;
						iClosest = cj;
						iClosestCase = which[k];
					}
				}

				if (iClosest == -1)
					continue;

				// [TODO] it would be better to preserve start/direction of
				//   longest path, if possible. Maybe make that an option?

				// ok we will join ci w/ iClosest. May need reverse one
				FillPolyline2d ljoin = Curves[iClosest];
				if (iClosestCase == 0) {
					l0.Reverse();
				} else if (iClosestCase == 1) {
					l0.Reverse();
					ljoin.Reverse();
				} else if (iClosestCase == 3) {
					ljoin.Reverse();
				}

				// now we are in straight-append order
				l0.AppendVertices(ljoin);
				Curves.RemoveAt(iClosest);

				// force check again w/ this curve
				ci--;
			}

		}


		static Segment2d get_case(FillPolyline2d l0, FillPolyline2d l1, int which) {
			if (which == 0)
				return new Segment2d(l0.Start, l1.Start);
			else if (which == 1)
				return new Segment2d(l0.Start, l1.End);
			else if (which == 2)
				return new Segment2d(l0.End, l1.Start);
			else
				return new Segment2d(l0.End, l1.End);
		}


	}
}

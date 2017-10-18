using System;
using System.Collections.Generic;
using System.Diagnostics;
using g3;

namespace gs
{
    /// <summary>
    /// Fill the input GPolygon with N offset shells. Generates list of 2d paths (.Shells) and
    /// also returns border polygons of "inner" region inside shells (.InnerPolygons)
    /// 
    /// Parameters:
    ///     .Layers:     number of shells
    ///     .ToolWidth:  thickness of 'tool', this is the space between subsequent shells
    ///     
    /// Options:
    ///     .InsetFromInputPolygon    : if true, initial shell is inset half a ToolWidth from input Polygon
    ///                                 (ie outer edge of tool would be "on" Polygon)
    ///     .InsetInnerPolygons       : if true, inner polygons are inset a tool-width from innermost shell (ie are
    ///                                 path you would put next shell on). If false, inner polygons lie on innermost shell.
    /// </summary>
	public class ShellsFillPolygon : IPathsFillPolygon
    {
		// polygon to fill
		public GeneralPolygon2d Polygon { get; set; }

		// parameters
		public double ToolWidth = 0.4;
		public double PathSpacing = 0.4;    // [TODO] this parameter is currently ignored?
		public int Layers = 2;

		public double DiscardTinyPerimterLengthMM = 1.0;
		public double DiscardTinyPolygonAreaMM2 = 1.0;

		// When offsets collide, we try to find polyline paths that will "fit"
		// This is multiplier on ToolWidth, we discard path segments within
		// ToolWidth*Multiplier distance from previous shell
		public double ToolWidthClipMultiplier = 0.8;


		// if true, we inset half of tool-width from Polygon,
		// otherwise first layer is polygon
		public bool InsetFromInputPolygon = true;

        // if true, inset InnerPolygons by a tool-width from last Shell,
        // otherwise InnerPolygons lies on that Shell
        public bool InsetInnerPolygons = true;

		// shell layers. Size of List == Layers
		public List<FillPaths2d> Shells { get; set; }
        public List<FillPaths2d> GetFillPaths() { return Shells; }

        // remaining interior polygons (to fill w/ other strategy, etc)
        public List<GeneralPolygon2d> InnerPolygons { get; set; }


        // [RMS] hack that lets us know this is an 'internal' shell which may be processed differently
        public bool IsInternalShell = false;

        // if true, we try to filter out self-overlaps (is expensive)
        public bool FilterSelfOverlaps = false;


		public ShellsFillPolygon(GeneralPolygon2d poly)
		{
			Polygon = poly;
			Shells = new List<FillPaths2d>();
		}


		public bool Compute()
		{
			bool enable_thin_check = false;
			double thin_check_offset = ToolWidth * 0.45;
			double thin_check_thresh_sqr = ToolWidth * 0.3;
			thin_check_thresh_sqr *= thin_check_thresh_sqr;

			// first shell is either polygon, or inset from that polygon
			List<GeneralPolygon2d> current = (InsetFromInputPolygon) ?
				ClipperUtil.ComputeOffsetPolygon(Polygon, -ToolWidth / 2, true) :
			   	new List<GeneralPolygon2d>() { Polygon };
            List<GeneralPolygon2d> current_prev = null;

			// convert previous layer to shell, and then compute next layer
			List<GeneralPolygon2d> failedShells = new List<GeneralPolygon2d>();
			List<GeneralPolygon2d> nextShellTooThin = new List<GeneralPolygon2d>();
			for (int i = 0; i < Layers; ++i ) {
                FillPaths2d paths = ShellPolysToPaths(current, i);
				Shells.Add(paths);

				List<GeneralPolygon2d> all_next = new List<GeneralPolygon2d>();
				foreach ( GeneralPolygon2d gpoly in current ) {
					List<GeneralPolygon2d> offsets =
						ClipperUtil.ComputeOffsetPolygon(gpoly, -ToolWidth, true);

					List<GeneralPolygon2d> filtered = new List<GeneralPolygon2d>();
					foreach (var v in offsets) {
						bool bTooSmall = (v.Perimeter < DiscardTinyPerimterLengthMM ||
										  v.Area < DiscardTinyPolygonAreaMM2);
						if (bTooSmall)
							continue;

						if ( enable_thin_check && is_too_thin(v, thin_check_offset, thin_check_thresh_sqr) )
							nextShellTooThin.Add(v);
						else
							filtered.Add(v);
					}

					if (filtered.Count == 0)
						failedShells.Add(gpoly);
					else
						all_next.AddRange(filtered);
				}

                current_prev = current;
                current = all_next;
			}


            // failedShells have no space for internal contours. But 
            // we might be able to fit a single line...
            //foreach (GeneralPolygon2d gpoly in failedShells) {
            //	if (gpoly.Perimeter < DiscardTinyPerimterLengthMM ||
            //		 gpoly.Area < DiscardTinyPolygonAreaMM2)
            //		continue;

            //	List<FillPolyline2d> thin_shells = thin_offset(gpoly);
            //	Shells[Shells.Count - 1].Append(thin_shells);
            //}


            // remaining inner polygons
            if (InsetInnerPolygons) {
                InnerPolygons = current;
                InnerPolygons.AddRange(nextShellTooThin);
            } else {
                InnerPolygons = current_prev;
                InnerPolygons.AddRange(nextShellTooThin);
            }
            return true;
		}



        public virtual FillPaths2d ShellPolysToPaths(List<GeneralPolygon2d> shell_polys, int nShell)
        {
            FillPaths2d paths = new FillPaths2d();

            foreach (GeneralPolygon2d shell in shell_polys) {
                if ( (nShell == 0 && IsInternalShell == false) || FilterSelfOverlaps == false ) {
                    paths.Append(shell);
                    continue;
                }

                PathOverlapRepair repair = new PathOverlapRepair();
                repair.OverlapRadius = PathSpacing*0.75;
                repair.Add(shell);
                repair.Compute();

                DGraph2Util.Curves c = DGraph2Util.ExtractCurves(repair.GetResultGraph());

                //if (IsInternalShell && c.Paths.Count == 1 && c.Paths[0].VertexCount > 10)
                //    Util.gDevAssert(false);

                foreach (var polygon in c.Loops)
                    paths.Append(polygon);
                foreach (var polyline in c.Paths)
                    paths.Append(new FillPolyline2d(polyline));
            }
            return paths;
        }





		public List<FillPolyline2d> thin_offset(GeneralPolygon2d p) {

			List<FillPolyline2d> result = new List<FillPolyline2d>();

			// to support non-hole thin offsets we need to return polylines
			if (p.Holes.Count == 0)
				return result;

			// compute desired offset from outer polygon
			GeneralPolygon2d outer = new GeneralPolygon2d(p.Outer);
			List<GeneralPolygon2d> offsets =
				ClipperUtil.ComputeOffsetPolygon(outer, -ToolWidth, true);
			if (offsets == null || offsets.Count == 0)
				return result;

			double clip_dist = ToolWidth * ToolWidthClipMultiplier;
			foreach (GeneralPolygon2d offset_poly in offsets) {
				List<FillPolyline2d> clipped = clip_to_band(offset_poly.Outer, p, clip_dist);
				result.AddRange(clipped);
			}

			return result;
		}



		public Polygon2d iterative_offset(GeneralPolygon2d poly, double fDist, int nSteps) {
			int N = poly.Outer.VertexCount;
			double max_step = fDist / nSteps;

			Polygon2d cur = new Polygon2d(poly.Outer);
			for (int i = 0; i < N; ++i ) {
				Vector2d n = cur.GetTangent(i).Perp;
				cur[i] = cur[i] + max_step * n;
			}

			return cur;
		}



		// (approximately) clip insetPoly to band around clipPoly.
		// vertices are discarded if outside clipPoly, or within clip_dist
		// remaining polylines are returned
		// In all-pass case currently returns polyline w/ explicit first==last vertices
		public List<FillPolyline2d> clip_to_band(Polygon2d insetpoly, GeneralPolygon2d clipPoly, double clip_dist) {

			double clipSqr = clip_dist * clip_dist;

			int N = insetpoly.VertexCount;
			Vector2d[] midline = new Vector2d[N];
			bool[] clipped = new bool[N];
			int nClipped = 0;
			for (int i = 0; i < N; ++i ) {
				Vector2d po = insetpoly[i];
				if (clipPoly.Contains(po) == false) {
					clipped[i] = true;
					nClipped++;
					continue;
				}

				int iHole, iSeg; double segT;
				double distSqr = clipPoly.DistanceSquared(po, out iHole, out iSeg, out segT);
				if ( distSqr < clipSqr ) {
					clipped[i] = true;
					nClipped++;
					continue;
				}

				// not ideal...
				midline[i] = po;
			}
			if (nClipped == N)
				return new List<FillPolyline2d>();
			if (nClipped == 0) {
				FillPolyline2d all = new FillPolyline2d(midline);
				all.AppendVertex(all.Start);
				return new List<FillPolyline2d>() { all };
			}

			return find_polygon_spans(midline, clipped);
		}



		// extract set of spans from poly where clipped=false
		List<FillPolyline2d> find_polygon_spans(Vector2d[] poly, bool[] clipped) 
		{
			// assumption: at least one vtx is clipped
			int iStart = 0;

			// handle no-wrap case
			if (clipped[iStart] == false && clipped[poly.Length-1] == true) {
				iStart = 0;
			} else {
				while (clipped[iStart] == true)     // find first non-clipped pt	
					iStart++;				
			}

			List<FillPolyline2d> result = new List<FillPolyline2d>();
			int iCur = iStart;
			bool done = false;

			while (done == false) {

				FillPolyline2d cur = new FillPolyline2d();
				do {
					cur.AppendVertex(poly[iCur]);
					iCur = (iCur + 1) % poly.Length;
				} while (clipped[iCur] == false && iCur != iStart);

				if ( cur.VertexCount > 1 )
					result.Add(cur);

				while (clipped[iCur] && iCur != iStart)
					iCur++;

				if (iCur == iStart)
					done = true;
			}

			return result;
		}



		// approximately check thickness of poly. For each segment, offset by check_offset*seg_normal,
		// then find distance to nearset point on poly. If distance_sqr is < mindist_sqr,
		// then we are below thin-tolerance.
		//
		// Currently returns true/false test, which is stupid...
		//
		// Will definitely fail on: squares (w/ short seg near edge), thin narrow bits... ??
		bool is_too_thin(GeneralPolygon2d poly, double check_offset, double mindist_sqr) 
		{
			Debug.Assert(mindist_sqr < 0.95 * check_offset * check_offset);

			bool failed = false;
			Action<Segment2d> seg_checkF = (seg) => {
				if (failed)
					return;
				if (seg.Length < 0.01)  // not robust if segment is too short
					return;			
				Vector2d n = -seg.Direction.Perp;
				Vector2d pt = seg.Center + check_offset * n;
				int iHole, iSeg; double segT;
				double dist_sqr = poly.DistanceSquared(pt, out iHole, out iSeg, out segT);
				if (dist_sqr < mindist_sqr)
					failed = true;
			};
			gParallel.ForEach(poly.AllSegmentsItr(), seg_checkF);
			return failed;
		}

	}
}

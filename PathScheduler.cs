using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	public class PathScheduler
	{
		public PathSetBuilder Builder;
		public SingleMaterialFFFSettings Settings;

		public enum SpeedModes {
			Careful, Rapid, MaxSpeed
		}
		public SpeedModes SpeedMode = SpeedModes.Careful; 



		public PathScheduler(PathSetBuilder builder, SingleMaterialFFFSettings settings)
		{
			Builder = builder;
			Settings = settings;
		}



		// dumbest possible scheduler...
		public virtual void Append(List<FillPaths2d> paths) {
			foreach (FillPaths2d polySet in paths) {
				foreach (Polygon2d loop in polySet.Loops) {
					AppendPolygon2d(loop);	
				}
				foreach (FillPolyline2d curve in polySet.Curves) {
					AppendPolyline2d(curve);
				}
			}
		}


		/// <summary>
		/// Assumes paths are "shells" contours, and that we want to print
		/// outermost shells last. 
		/// [TODO] smarter handling of nested shell-sets
		/// </summary>
		public virtual void AppendShells(List<FillPaths2d> paths)
		{
			foreach (FillPaths2d polySet in paths) {
				if (polySet.Curves.Count > 0)
					throw new Exception("PathScheduler.AppendShells: don't support open-curves here yet");
			}

			List<Polygon2d> OuterLoops = paths[0].Loops;
			for (int i = 1; i < paths.Count; ++i) {
				foreach (Polygon2d loop in paths[i].Loops)
					AppendPolygon2d(loop);
			}

			// add outermost loops
			foreach (Polygon2d poly in OuterLoops)
				AppendPolygon2d(poly);
		}



		// [TODO] no reason we couldn't start on edge midpoint??
		public void AppendPolygon2d(Polygon2d poly) {
			Vector3d currentPos = Builder.Position;
			Vector2d currentPos2 = currentPos.xy;

			int N = poly.VertexCount;
			if (N < 2)
				throw new Exception("PathScheduler.AppendPolygon2d: degenerate curve!");

			int iNearest = CurveUtils2.FindNearestVertex(currentPos2, poly);

			Vector2d startPt = poly[iNearest];
			Builder.AppendTravel(startPt, Settings.RapidTravelSpeed);

			List<Vector2d> loopV = new List<Vector2d>(N + 1);
			for (int i = 0; i <= N; i++ ) {
				int k = (iNearest + i) % N;
				loopV.Add(poly[k]);
			}

			// [TODO] speed here...
			Builder.AppendExtrude(loopV, Settings.CarefulExtrudeSpeed);
		}




		// [TODO] would it ever make sense to break polyline to avoid huge travel??
		public void AppendPolyline2d(FillPolyline2d curve)
		{
			Vector3d currentPos = Builder.Position;
			Vector2d currentPos2 = currentPos.xy;

			int N = curve.VertexCount;
			if (N < 2)
				throw new Exception("PathScheduler.AppendPolyline2d: degenerate curve!");

			int iNearest = 0;
			bool bReverse = false;
			if (curve.Start.DistanceSquared(currentPos2) > curve.End.DistanceSquared(currentPos2)) {
				iNearest = N - 1;
				bReverse = true;
			}

			Vector2d startPt = curve[iNearest];
			Builder.AppendTravel(startPt, Settings.RapidTravelSpeed);

			List<Vector2d> loopV;
			List<Index3i> flags = null;
			if (bReverse) {
				loopV = new List<Vector2d>(N);
				for (int i = N - 1; i >= 0; --i)
					loopV.Add(curve[i]);
				if (curve.HasFlags) {
					flags = new List<Index3i>(N);
					for (int i = N - 1; i >= 0; --i)
						flags.Add(curve.GetFlag(i));
				}
			} else {
				loopV = new List<Vector2d>(curve);
				if (curve.HasFlags)
					flags = new List<Index3i>(curve.Flags());
			}

			// [TODO] speed here...
			double useSpeed = (SpeedMode == SpeedModes.Careful) ?
				Settings.CarefulExtrudeSpeed : Settings.RapidExtrudeSpeed;
			Builder.AppendExtrude(loopV, useSpeed, flags);
		}


	}
}

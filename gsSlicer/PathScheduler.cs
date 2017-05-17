using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	public class PathScheduler
	{
		public PathSetBuilder Builder;
		public SingleMaterialFFFSettings Settings;



		public PathScheduler(PathSetBuilder builder, SingleMaterialFFFSettings settings)
		{
			Builder = builder;
			Settings = settings;
		}



		// dumbest possible scheduler...
		public void Append(List<FillPaths2d> paths) {

			Vector3d currentPos = Builder.Position;

			// [TODO] assume we are at same z-height?

			foreach (FillPaths2d polySet in paths) {
				foreach (Polygon2d loop in polySet.Loops) {
					AppendPolygon2d(loop);	
				}
				foreach (PolyLine2d curve in polySet.Curves) {
					AppendPolyline2d(curve);
				}
			}

		}






		// [TODO] no reason we couldn't start on edge midpoint??
		public void AppendPolygon2d(Polygon2d poly) {
			Vector3d currentPos = Builder.Position;
			Vector2d currentPos2 = currentPos.xy;

			int N = poly.VertexCount;
			int iNearest = CurveUtils2.FindNearestVertex(currentPos2, poly);

			Vector2d startPt = poly[iNearest];
			Builder.AppendTravel(startPt, Settings.RapidTravelSpeed);

			List<Vector2d> loopV = new List<Vector2d>(N + 1);
			for (int i = 0; i <= N; i++ ) {
				int k = (iNearest + i) % N;
				loopV.Add(poly[k]);
			}

			// [TODO] speed here...
			Builder.AppendExtrude(loopV, Settings.FirstLayerExtrudeSpeed);
		}




		// [TODO] would it ever make sense to break polyline to avoid huge travel??
		public void AppendPolyline2d(PolyLine2d curve)
		{
			Vector3d currentPos = Builder.Position;
			Vector2d currentPos2 = currentPos.xy;

			int N = curve.VertexCount;
			int iNearest = 0;
			bool bReverse = false;
			if (curve.Start.DistanceSquared(currentPos2) > curve.End.DistanceSquared(currentPos2)) {
				iNearest = N - 1;
				bReverse = true;
			}

			Vector2d startPt = curve[iNearest];
			Builder.AppendTravel(startPt, Settings.RapidTravelSpeed);

			List<Vector2d> loopV;
			if (bReverse) {
				loopV = new List<Vector2d>(N);
				for (int i = N - 1; i >= 0; --i)
					loopV.Add(curve[i]);
			} else {
				loopV = new List<Vector2d>(curve);
			}

			// [TODO] speed here...
			Builder.AppendExtrude(loopV, Settings.FirstLayerExtrudeSpeed);
		}


	}
}

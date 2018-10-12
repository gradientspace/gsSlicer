using System;
using System.Collections.Generic;
using g3;

namespace gs
{
    public enum SchedulerSpeedHint
    {
        Careful, Default, Rapid, MaxSpeed
    }


    public interface IFillPathScheduler2d
    {
        void AppendCurveSets(List<FillCurveSet2d> paths);

        SchedulerSpeedHint SpeedHint { get; set; }
    }


    // dumbest possible scheduler...
    public class SequentialScheduler2d : IFillPathScheduler2d
	{
		public ToolpathSetBuilder Builder;
		public SingleMaterialFFFSettings Settings;

        public bool ExtrudeOnShortTravels = false;
        public double ShortTravelDistance = 0;

        // optional function we will call when curve sets are appended
        public Action<List<FillCurveSet2d>, SequentialScheduler2d> OnAppendCurveSetsF = null;


        public SequentialScheduler2d(ToolpathSetBuilder builder, SingleMaterialFFFSettings settings)
		{
			Builder = builder;
			Settings = settings;
		}


        SchedulerSpeedHint speed_hint = SchedulerSpeedHint.Default;
        public virtual SchedulerSpeedHint SpeedHint {
            get { return speed_hint; }
            set { speed_hint = value; }
        }



        public virtual void AppendCurveSets(List<FillCurveSet2d> paths) {
            if (OnAppendCurveSetsF != null)
                OnAppendCurveSetsF(paths, this);

            foreach (FillCurveSet2d polySet in paths) {
				foreach (FillPolygon2d loop in polySet.Loops) {
					AppendPolygon2d(loop);	
				}
				foreach (FillPolyline2d curve in polySet.Curves) {
					AppendPolyline2d(curve);
				}
			}
		}


		// [TODO] no reason we couldn't start on edge midpoint??
		public virtual void AppendPolygon2d(FillPolygon2d poly)
        {
            Vector3d currentPos = Builder.Position;
            Vector2d currentPos2 = currentPos.xy;

            int N = poly.VertexCount;
            if (N < 2)
                throw new Exception("PathScheduler.AppendPolygon2d: degenerate curve!");

            int iNearest = CurveUtils2.FindNearestVertex(currentPos2, poly.Vertices);

            Vector2d startPt = poly[iNearest];

            AppendTravel(currentPos2, startPt);

            List<Vector2d> loopV = new List<Vector2d>(N + 1);
            for (int i = 0; i <= N; i++)
            {
                int k = (iNearest + i) % N;
                loopV.Add(poly[k]);
            }

            double useSpeed = select_speed(poly);

            Builder.AppendExtrude(loopV, useSpeed, poly.TypeFlags, null);
        }

        private void AppendTravel(Vector2d startPt, Vector2d endPt)
        {
            double travelDistance = startPt.Distance(endPt);

            // a travel may require a retract, which we might want to skip
            if (ExtrudeOnShortTravels &&
                travelDistance < ShortTravelDistance)
            {
                Builder.AppendExtrude(endPt, Settings.RapidTravelSpeed);
            }
            else if (Settings.TravelLiftEnabled &&
                travelDistance > Settings.TravelLiftDistanceThreshold)
            {
                Builder.AppendZChange(Settings.TravelLiftHeight, Settings.ZTravelSpeed, ToolpathTypes.Travel);
                Builder.AppendTravel(endPt, Settings.RapidTravelSpeed);
                Builder.AppendZChange(-Settings.TravelLiftHeight, Settings.ZTravelSpeed, ToolpathTypes.Travel);
            }
            else
            {
                Builder.AppendTravel(endPt, Settings.RapidTravelSpeed);
            }
        }




        // [TODO] would it ever make sense to break polyline to avoid huge travel??
        public virtual void AppendPolyline2d(FillPolyline2d curve)
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
            AppendTravel(currentPos2, startPt);

			List<Vector2d> loopV;
			List<TPVertexFlags> flags = null;
			if (bReverse) {
				loopV = new List<Vector2d>(N);
				for (int i = N - 1; i >= 0; --i)
					loopV.Add(curve[i]);
				if (curve.HasFlags) {
					flags = new List<TPVertexFlags>(N);
					for (int i = N - 1; i >= 0; --i)
						flags.Add(curve.GetFlag(i));
				}
			} else {
				loopV = new List<Vector2d>(curve);
				if (curve.HasFlags)
					flags = new List<TPVertexFlags>(curve.Flags());
			}

            double useSpeed = select_speed(curve);

			Vector2d dimensions = GCodeUtil.UnspecifiedDimensions;
			if (curve.CustomThickness > 0)
				dimensions.x = curve.CustomThickness;

            Builder.AppendExtrude(loopV, useSpeed, dimensions, curve.TypeFlags, flags);
		}



        // 1) If we have "careful" speed hint set, use CarefulExtrudeSpeed
        //       (currently this is only set on first layer)
        // 2) if this is an outer perimeter, scale by outer perimeter speed multiplier
        // 3) if we are being "careful" and this is support, also use that multiplier
        //       (bit of a hack, currently means on first layer we do support extra slow)
        double select_speed(FillCurve2d pathCurve)
        {
            bool bIsSupport = pathCurve.HasTypeFlag(FillTypeFlags.SupportMaterial);
            bool bIsOuterPerimeter = pathCurve.HasTypeFlag(FillTypeFlags.OuterPerimeter);
            bool bCareful = (SpeedHint == SchedulerSpeedHint.Careful);
            double useSpeed = bCareful ? Settings.CarefulExtrudeSpeed : Settings.RapidExtrudeSpeed;
            if (bIsOuterPerimeter || (bCareful && bIsSupport))
                useSpeed *= Settings.OuterPerimeterSpeedX;

			bool bIsBridgeSupport = pathCurve.HasTypeFlag(FillTypeFlags.BridgeSupport);
			if (bIsBridgeSupport)
				useSpeed = Settings.CarefulExtrudeSpeed * Settings.BridgeExtrudeSpeedX;

            return useSpeed;
        }


	}
}

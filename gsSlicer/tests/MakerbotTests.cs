using System;
using System.Collections.Generic;
using System.Diagnostics;
using g3;

namespace gs
{
	using LinearPath = LinearPath3<PathVertex>;

	public class MakerbotTests
	{

		public static GCodeFile SimpleFillTest()
		{
			GCodeFileAccumulator fileAccum = new GCodeFileAccumulator();
			GCodeBuilder builder = new GCodeBuilder(fileAccum);

			MakerbotSettings settings = new MakerbotSettings();

			MakerbotCompiler cc = new MakerbotCompiler(builder, settings);

			cc.Begin();

			double StepY = settings.FillPathSpacingMM;

			PathSetBuilder paths = new PathSetBuilder();
			paths.Initialize(cc.NozzlePosition);
			Vector3d currentPos = paths.Position;

			currentPos = paths.AppendZChange(settings.LayerHeightMM, settings.ZTravelSpeed);

			currentPos = paths.AppendTravel(
				new Vector2d(-50, 0), settings.RapidMoveSpeed);

			Vector2d pos = currentPos.xy;
			List<Vector2d> fill = new List<Vector2d>();
			fill.Add(pos);
			for (int k = 0; k < 5; ++k) {
				pos.x += 100; 		fill.Add(pos);
				pos.y += StepY; 	fill.Add(pos);
				pos.x -= 100;		fill.Add(pos);
				pos.y += StepY; 	fill.Add(pos);
			}
			pos.x += 100; 			fill.Add(pos);
			currentPos = paths.AppendExtrude(fill, settings.FirstLayerExtrudeSpeed);

			cc.AppendPaths(paths.Paths);

			cc.End();

			return fileAccum.File;
		}
	}
}

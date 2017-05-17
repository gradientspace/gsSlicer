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
				new Vector2d(-50, 0), settings.RapidTravelSpeed);

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






		public static GCodeFile SimpleShellsTest()
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

			// layer-up
			currentPos = paths.AppendZChange(settings.LayerHeightMM, settings.ZTravelSpeed);

			PathScheduler scheduler = new PathScheduler(paths, settings);

			Polygon2d poly = new Polygon2d();
			double r = 10;
			poly.AppendVertex(new Vector2d(-r,-r));
			poly.AppendVertex(new Vector2d(r,-r));
			poly.AppendVertex(new Vector2d(r, r));
			poly.AppendVertex(new Vector2d(-r, r));
			GeneralPolygon2d shape = new GeneralPolygon2d() { Outer = poly };

			ShellsFillPolygon shells_gen = new ShellsFillPolygon(shape);
			shells_gen.PathSpacing = settings.FillPathSpacingMM;
			shells_gen.ToolWidth = settings.NozzleDiamMM;
			shells_gen.Layers = 5;
			shells_gen.Compute();

			scheduler.Append(shells_gen.Shells);

			foreach (GeneralPolygon2d infill_poly in shells_gen.InnerPolygons) {
				DenseLinesFillPolygon infill_gen = new DenseLinesFillPolygon(infill_poly) {
					InsetFromInputPolygon = false,
					PathSpacing = settings.FillPathSpacingMM,
					ToolWidth = settings.NozzleDiamMM
				};
				infill_gen.Compute();
				scheduler.Append(infill_gen.Paths);
			}


			currentPos = paths.Position;


			cc.AppendPaths(paths.Paths);

			cc.End();

			return fileAccum.File;
		}


	}
}

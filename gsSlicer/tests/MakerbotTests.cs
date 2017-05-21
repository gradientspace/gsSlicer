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

			System.Console.WriteLine("[MakerbotTests] total extrude length: {0}", cc.ExtruderA);
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

			Polygon2d hole = Polygon2d.MakeCircle(r / 2, 6, 30*MathUtil.Deg2Rad);
			hole.Reverse();
			shape.AddHole(hole);

			ShellsFillPolygon shells_gen = new ShellsFillPolygon(shape);
			shells_gen.PathSpacing = settings.FillPathSpacingMM;
			shells_gen.ToolWidth = settings.NozzleDiamMM;
			shells_gen.Layers = 2;
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

			cc.AppendPaths(paths.Paths);

			cc.End();

			System.Console.WriteLine("[MakerbotTests] total extrude length: {0}", cc.ExtruderA);
			return fileAccum.File;
		}






		public static GCodeFile ShellsPolygonTest(GeneralPolygon2d shape)
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

			ShellsFillPolygon shells_gen = new ShellsFillPolygon(shape);
			shells_gen.PathSpacing = settings.FillPathSpacingMM;
			shells_gen.ToolWidth = settings.NozzleDiamMM;
			shells_gen.Layers = 2;
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

			cc.AppendPaths(paths.Paths);

			cc.End();

			System.Console.WriteLine("[MakerbotTests] total extrude length: {0}", cc.ExtruderA);
			return fileAccum.File;
		}







		public static GCodeFile StackedPolygonTest(GeneralPolygon2d shape, int nLayers)
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


			ShellsFillPolygon shells_gen = new ShellsFillPolygon(shape);
			shells_gen.PathSpacing = settings.FillPathSpacingMM;
			shells_gen.ToolWidth = settings.NozzleDiamMM;
			shells_gen.Layers = 2;
			shells_gen.Compute();

			List<FillPaths2d> infill_paths = new List<FillPaths2d>();
			foreach (GeneralPolygon2d infill_poly in shells_gen.InnerPolygons) {
				DenseLinesFillPolygon infill_gen = new DenseLinesFillPolygon(infill_poly) {
					InsetFromInputPolygon = false,
					PathSpacing = settings.FillPathSpacingMM,
					ToolWidth = settings.NozzleDiamMM
				};
				infill_gen.Compute();
				infill_paths.AddRange(infill_gen.Paths);
			}

			for (int i = 0; i < nLayers; ++i) {
				// layer-up
				paths.AppendZChange(settings.LayerHeightMM, settings.ZTravelSpeed);

				// add paths
				PathScheduler scheduler = new PathScheduler(paths, settings);
				scheduler.Append(shells_gen.Shells);
				scheduler.Append(infill_paths);
			}

			cc.AppendPaths(paths.Paths);
			cc.End();

			System.Console.WriteLine("[MakerbotTests] total extrude length: {0}", cc.ExtruderA);
			return fileAccum.File;
		}





		public static GCodeFile StackedScaledPolygonTest(GeneralPolygon2d shapeIn, int nLayers, double fTopScale)
		{
			if (fTopScale < 0.25 || fTopScale > 1.5)
				throw new Exception("not a good idea?");

			GCodeFileAccumulator fileAccum = new GCodeFileAccumulator();
			GCodeBuilder builder = new GCodeBuilder(fileAccum);

			MakerbotSettings settings = new MakerbotSettings();

			MakerbotCompiler cc = new MakerbotCompiler(builder, settings);

			cc.Begin();

			double StepY = settings.FillPathSpacingMM;

			for (int i = 0; i < nLayers; ++i ) {
				double t = (double)i / (double)(nLayers-1);
				double scale = MathUtil.Lerp(1, fTopScale, t);

				PathSetBuilder paths = new PathSetBuilder();
				paths.Initialize(cc.NozzlePosition);
				Vector3d currentPos = paths.Position;

				// layer-up
				currentPos = paths.AppendZChange(settings.LayerHeightMM, settings.ZTravelSpeed);

				PathScheduler scheduler = new PathScheduler(paths, settings);

				GeneralPolygon2d shape = new GeneralPolygon2d(shapeIn);
				shape.Scale(scale*Vector2d.One, Vector2d.Zero);

				ShellsFillPolygon shells_gen = new ShellsFillPolygon(shape);
				shells_gen.PathSpacing = settings.FillPathSpacingMM;
				shells_gen.ToolWidth = settings.NozzleDiamMM;
				shells_gen.Layers = 2;
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

				cc.AppendPaths(paths.Paths);

			}

			cc.End();

			System.Console.WriteLine("[MakerbotTests] total extrude length: {0}", cc.ExtruderA);
			return fileAccum.File;
		}








		public static GCodeFile SliceMeshTest(DMesh3 mesh)
		{

			GCodeFileAccumulator fileAccum = new GCodeFileAccumulator();
			GCodeBuilder builder = new GCodeBuilder(fileAccum);
			MakerbotSettings settings = new MakerbotSettings();

			AxisAlignedBox3d bounds = mesh.CachedBounds;
			int nLayers = (int)(bounds.Diagonal.z / settings.LayerHeightMM);


			MakerbotCompiler cc = new MakerbotCompiler(builder, settings);

			cc.Begin();

			double StepY = settings.FillPathSpacingMM;


			for (int i = 0; i <= nLayers; ++i) {
				System.Console.WriteLine("Layer {0} of {1}", i, nLayers);

				double z = ((double)i + 0.5) * settings.LayerHeightMM;
				DMesh3 sliceMesh = new DMesh3(mesh);
				MeshPlaneCut cut = new MeshPlaneCut(sliceMesh, new Vector3d(0, 0, z), Vector3d.AxisZ);
				cut.Cut();

				// [TODO] holes ?

				List<GeneralPolygon2d> polygons = new List<GeneralPolygon2d>();
				foreach ( EdgeLoop loop in cut.CutLoops ) {
					Polygon2d poly = new Polygon2d();
					foreach ( int vid in loop.Vertices ) {
						Vector3d v = sliceMesh.GetVertex(vid);
						poly.AppendVertex(v.xy);
					}
					polygons.Add(new GeneralPolygon2d(poly));
				}


				PathSetBuilder paths = new PathSetBuilder();
				paths.Initialize(cc.NozzlePosition);
				Vector3d currentPos = paths.Position;

				// layer-up
				currentPos = paths.AppendZChange(settings.LayerHeightMM, settings.ZTravelSpeed);

				PathScheduler scheduler = new PathScheduler(paths, settings);

				foreach(GeneralPolygon2d shape in polygons) {
					ShellsFillPolygon shells_gen = new ShellsFillPolygon(shape);
					shells_gen.PathSpacing = settings.FillPathSpacingMM;
					shells_gen.ToolWidth = settings.NozzleDiamMM;
					shells_gen.Layers = 2;
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
				}

				cc.AppendPaths(paths.Paths);

			}

			cc.End();

			System.Console.WriteLine("[MakerbotTests] total extrude length: {0}", cc.ExtruderA);
			return fileAccum.File;
		}


	}
}

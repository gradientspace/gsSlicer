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
			currentPos = paths.AppendExtrude(fill, settings.CarefulExtrudeSpeed);

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

			MeshPlanarSlicer slicer = new MeshPlanarSlicer();
			slicer.LayerHeightMM = settings.LayerHeightMM;
			slicer.AddMesh(mesh);

			PlanarSliceStack stack = slicer.Compute();
			int nLayers = stack.Slices.Count;

			int RoofFloorLayers = 2;
			double InfillScale = 3.0;
			double[] infill_angles = new double[] { -45, 45 };

			MakerbotCompiler cc = new MakerbotCompiler(builder, settings);

			cc.Begin();

			for (int i = 0; i < nLayers; ++i) {
				System.Console.WriteLine("Layer {0} of {1}", i, nLayers);

				PlanarSlice slice = stack.Slices[i];

				PathSetBuilder paths = new PathSetBuilder();
				paths.Initialize(cc.NozzlePosition);
				Vector3d currentPos = paths.Position;

				// layer-up
				currentPos = paths.AppendZChange(settings.LayerHeightMM, settings.ZTravelSpeed);

				bool is_infill = (i >= RoofFloorLayers && i < nLayers - RoofFloorLayers-1);
				double fillScale = (is_infill) ? InfillScale : 1.0f;

				PathScheduler scheduler = new PathScheduler(paths, settings);

				foreach(GeneralPolygon2d shape in slice.Solids) {
					ShellsFillPolygon shells_gen = new ShellsFillPolygon(shape);
					shells_gen.PathSpacing = settings.FillPathSpacingMM;
					shells_gen.ToolWidth = settings.NozzleDiamMM;
					shells_gen.Layers = 2;
					shells_gen.Compute();

					scheduler.Append(shells_gen.Shells);

					foreach (GeneralPolygon2d infill_poly in shells_gen.InnerPolygons) {
						DenseLinesFillPolygon infill_gen = new DenseLinesFillPolygon(infill_poly) {
							InsetFromInputPolygon = false,
							PathSpacing = fillScale * settings.FillPathSpacingMM,
							ToolWidth = settings.NozzleDiamMM,
							AngleDeg = infill_angles[i % infill_angles.Length]
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








		public static GCodeFile SliceMeshTest_Roofs(DMesh3 mesh)
		{
			GCodeFileAccumulator fileAccum = new GCodeFileAccumulator();
			GCodeBuilder builder = new GCodeBuilder(fileAccum);
			MakerbotSettings settings = new MakerbotSettings();

			MeshPlanarSlicer slicer = new MeshPlanarSlicer();
			slicer.LayerHeightMM = settings.LayerHeightMM;
			slicer.AddMesh(mesh);

			PlanarSliceStack stack = slicer.Compute();
			int nLayers = stack.Slices.Count;


			int RoofFloorLayers = 2;
			double InfillScale = 3.0;
			double[] infill_angles = new double[] { -45, 45 };


			MakerbotCompiler cc = new MakerbotCompiler(builder, settings);

			cc.Begin();


			// compute shells for each layer
			List<ShellsFillPolygon>[] LayerShells = new List<ShellsFillPolygon>[nLayers];
			gParallel.ForEach(Interval1i.Range(nLayers), (layeri) => {
				PlanarSlice slice = stack.Slices[layeri];
				LayerShells[layeri] = new List<ShellsFillPolygon>();

				foreach (GeneralPolygon2d shape in slice.Solids) {
					ShellsFillPolygon shells_gen = new ShellsFillPolygon(shape);
					shells_gen.PathSpacing = settings.FillPathSpacingMM;
					shells_gen.ToolWidth = settings.NozzleDiamMM;
					shells_gen.Layers = 2;
					shells_gen.Compute();
					LayerShells[layeri].Add(shells_gen);
				}
			});



			for (int i = 0; i < nLayers; ++i) {
				System.Console.WriteLine("Layer {0} of {1}", i, nLayers);

				PlanarSlice slice = stack.Slices[i];

				PathSetBuilder paths = new PathSetBuilder();
				paths.Initialize(cc.NozzlePosition);
				Vector3d currentPos = paths.Position;

				// layer-up
				currentPos = paths.AppendZChange(settings.LayerHeightMM, settings.ZTravelSpeed);

				bool is_infill = (i >= RoofFloorLayers && i < nLayers - RoofFloorLayers - 1);
				double fillScale = (is_infill) ? InfillScale : 1.0f;

				PathScheduler scheduler = new PathScheduler(paths, settings);

				// should be parameterizable? this is 45 degrees...
				//   (is it? 45 if nozzlediam == layerheight...)
				double fOverhangAllowance = 0.5 * settings.NozzleDiamMM;

				// construct region that needs to be solid for "roofs".
				// This is the intersection of infill polygons for the next N layers
				List<GeneralPolygon2d> roof_cover = new List<GeneralPolygon2d>();
				if (is_infill) {
					foreach (ShellsFillPolygon shells in LayerShells[i + 1])
						roof_cover.AddRange(shells.InnerPolygons);

					// If we want > 1 roof layer, we need to look further ahead.
					// The full area we need to print as "roof" is the infill minus
					// the intersection of the infill areas above
					for (int k = 2; k <= RoofFloorLayers; ++k) {
						int ri = i + k;
						if (ri < LayerShells.Length) {
							List<GeneralPolygon2d> infillN = new List<GeneralPolygon2d>();
							foreach (ShellsFillPolygon shells in LayerShells[ri])
								infillN.AddRange(shells.InnerPolygons);

							roof_cover = ClipperUtil.Intersection(roof_cover, infillN);
						}
					}

					// add overhang allowance. Technically any non-vertical surface will result in
					// non-empty roof regions. However we do not need to explicitly support roofs
					// until they are "too horizontal". 
					roof_cover = ClipperUtil.MiterOffset(roof_cover, fOverhangAllowance);
				}



				// construct region that needs to be solid for "floors"
				List<GeneralPolygon2d> floor_cover = new List<GeneralPolygon2d>();
				if (is_infill) {
					foreach (ShellsFillPolygon shells in LayerShells[i-1])
						floor_cover.AddRange(shells.InnerPolygons);

					// If we want > 1 floor layer, we need to look further back.
					for (int k = 2; k <= RoofFloorLayers; ++k) {
						int ri = i - k;
						if (ri > 0) {
							List<GeneralPolygon2d> infillN = new List<GeneralPolygon2d>();
							foreach (ShellsFillPolygon shells in LayerShells[ri])
								infillN.AddRange(shells.InnerPolygons);

							floor_cover = ClipperUtil.Intersection(floor_cover, infillN);
						}
					}

					// add overhang allowance. 
					floor_cover = ClipperUtil.MiterOffset(floor_cover, fOverhangAllowance);
				}


				List<ShellsFillPolygon> curShells = LayerShells[i];
				for (int si = 0; si < curShells.Count; si++) {
					ShellsFillPolygon shells_gen = curShells[si];

					scheduler.Append(shells_gen.Shells);

					// construct infill poly list
					List<GeneralPolygon2d> infillPolys = new List<GeneralPolygon2d>();
					List<GeneralPolygon2d> solidFillPolys = shells_gen.InnerPolygons;
					if (is_infill) {
						infillPolys = shells_gen.InnerPolygons;
						List<GeneralPolygon2d> roofPolys = ClipperUtil.Difference(infillPolys, roof_cover);
						List<GeneralPolygon2d> floorPolys = ClipperUtil.Difference(infillPolys, floor_cover);
						solidFillPolys = ClipperUtil.Union(roofPolys, floorPolys);
						if (solidFillPolys == null)
							solidFillPolys = new List<GeneralPolygon2d>();

						// [TODO] I think maybe we should actually do another set of contours for the
						// solid region. At least one. This gives the solid & infill something to
						// connect to, and gives the contours above a continuous bonding thread

						// subtract solid fill from infill regions. However because we *don't*
						// inset fill regions, we need to subtract (solid+offset), so that
						// infill won't overlap solid region
						if ( solidFillPolys.Count > 0 ) {
							List<GeneralPolygon2d> solidWithBorder = 
								ClipperUtil.MiterOffset(solidFillPolys, settings.NozzleDiamMM);
							infillPolys = ClipperUtil.Difference(infillPolys, solidWithBorder);
						}
					}

					// fill solid regions
					foreach (GeneralPolygon2d solid_poly in solidFillPolys) {
						DenseLinesFillPolygon solid_gen = new DenseLinesFillPolygon(solid_poly) {
							InsetFromInputPolygon = false,
							PathSpacing = settings.FillPathSpacingMM,
							ToolWidth = settings.NozzleDiamMM,
							AngleDeg = infill_angles[i % infill_angles.Length]
						};
						solid_gen.Compute();
						scheduler.Append(solid_gen.Paths);
					}

					// fill infill regions
					foreach (GeneralPolygon2d infill_poly in infillPolys) {
						DenseLinesFillPolygon infill_gen = new DenseLinesFillPolygon(infill_poly) {
							InsetFromInputPolygon = false,
							PathSpacing = InfillScale * settings.FillPathSpacingMM,
							ToolWidth = settings.NozzleDiamMM,
							AngleDeg = infill_angles[i % infill_angles.Length]
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









		public static GCodeFile InfillBoxTest()
		{
			double r = 20;				// box 'radius'
			int HeightLayers = 10;
			int RoofFloorLayers = 2;
			double InfillScale = 6.0;
			double[] infill_angles = new double[] { -45, 45 };
			//double[] infill_angles = new double[] { -45 };
			int infill_layer_k = 0;
			bool enable_rapid = true;
			bool enable_layer_offset = true;

			GCodeFileAccumulator fileAccum = new GCodeFileAccumulator();
			GCodeBuilder builder = new GCodeBuilder(fileAccum);
			MakerbotSettings settings = new MakerbotSettings();


			MakerbotCompiler cc = new MakerbotCompiler(builder, settings);

			cc.Begin();


			Polygon2d poly = new Polygon2d();
			poly.AppendVertex(new Vector2d(-r, -r));
			poly.AppendVertex(new Vector2d(r, -r));
			poly.AppendVertex(new Vector2d(r, r));
			poly.AppendVertex(new Vector2d(-r, r));
			GeneralPolygon2d gpoly = new GeneralPolygon2d() { Outer = poly };
			List<GeneralPolygon2d> polygons = new List<GeneralPolygon2d>() { gpoly };

			//GeneralPolygon2d sub = new GeneralPolygon2d(Polygon2d.MakeCircle(r / 2, 12));
			////sub.Translate(r * Vector2d.One);
			//List<GeneralPolygon2d> result =
			//	ClipperUtil.PolygonBoolean(polygons, sub, ClipperUtil.BooleanOp.Difference);
			//polygons = result;

			for (int i = 0; i < HeightLayers; ++i) {
				System.Console.WriteLine("Layer {0} of {1}", i, HeightLayers);

				//bool is_infill = (i >= RoofFloorLayers && i < HeightLayers - RoofFloorLayers);
				bool is_infill = (i >= RoofFloorLayers);
				double fillScale = (is_infill) ? InfillScale : 1.0f;

				PathSetBuilder paths = new PathSetBuilder();
				paths.Initialize(cc.NozzlePosition);
				Vector3d currentPos = paths.Position;

				// layer-up
				currentPos = paths.AppendZChange(settings.LayerHeightMM, settings.ZTravelSpeed);

				PathScheduler scheduler = new PathScheduler(paths, settings);
				if (is_infill && enable_rapid)
					scheduler.SpeedMode = PathScheduler.SpeedModes.Rapid;

				foreach (GeneralPolygon2d shape in polygons) {
					ShellsFillPolygon shells_gen = new ShellsFillPolygon(shape);
					shells_gen.PathSpacing = settings.FillPathSpacingMM;
					shells_gen.ToolWidth = settings.NozzleDiamMM;
					shells_gen.Layers = 2;
					shells_gen.Compute();

					scheduler.AppendShells(shells_gen.Shells);

					foreach (GeneralPolygon2d infill_poly in shells_gen.InnerPolygons) {
						DenseLinesFillPolygon infill_gen = new DenseLinesFillPolygon(infill_poly) {
							InsetFromInputPolygon = false,
							PathSpacing = fillScale * settings.FillPathSpacingMM,
							ToolWidth = settings.NozzleDiamMM,
							AngleDeg = infill_angles[infill_layer_k],
							PathShift = (enable_layer_offset == false || i % 2 == 0) 
								? 0 : (fillScale * settings.FillPathSpacingMM *(0.5))
						};
						infill_gen.Compute();
						scheduler.Append(infill_gen.Paths);
					}
				}

				cc.AppendPaths(paths.Paths);
				infill_layer_k = (infill_layer_k + 1) % infill_angles.Length;
			}

			cc.End();

			System.Console.WriteLine("[MakerbotTests] total extrude length: {0}", cc.ExtruderA);
			return fileAccum.File;
		}



	}
}

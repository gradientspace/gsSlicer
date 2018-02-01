using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using g3;

namespace gs
{

	/// <summary>
	/// PrintLayerData is set of information for a single print layer
	/// </summary>
	public class PrintLayerData
	{
		public int layer_i;
		public PlanarSlice Slice;
		public SingleMaterialFFFSettings Settings;

		public PrintLayerData PreviousLayer;

		public PathSetBuilder PathAccum;
		public IPathScheduler Scheduler;

		public List<IShellsFillPolygon> ShellFills;

		public TemporalPathHash Spatial;

		public PrintLayerData(int layer_i, PlanarSlice slice, SingleMaterialFFFSettings settings) {
			this.layer_i = layer_i;
			Slice = slice;
			Settings = settings;
			Spatial = new TemporalPathHash();
		}
	}




    /// <summary>
    /// This is the top-level class that generates a GCodeFile for a stack of slices.
    /// Currently must subclass to provide resulting GCodeFile.
    /// </summary>
    public abstract class ThreeAxisPrintGenerator
    {
        // Data structures that must be provided by client
        protected PrintMeshAssembly PrintMeshes;
        protected PlanarSliceStack Slices;
        protected ThreeAxisPrinterCompiler Compiler;
        public SingleMaterialFFFSettings Settings;      // public because you could modify
                                                        // this during process, ie in BeginLayerF
                                                        // to implement per-layer settings

        // available after calling Generate()
        public GCodeFile Result;


		/*
		 * Customizable functions you can use to configure/modify slicer behavior
		 */

        // replace this with your own error message handler
        public Action<string, string> ErrorF = (message, stack_trace) => {
            System.Console.WriteLine("[EXCEPTION] ThreeAxisPrintGenerator: " + message + "\nSTACK TRACE: " + stack_trace);
        };


		// Replace this if you want to cusotmize PrintLayerData type
		public Func<int, PlanarSlice, SingleMaterialFFFSettings, PrintLayerData> PrintLayerDataFactoryF;

		// Replace this to use a different path builder
		public Func<PrintLayerData, PathSetBuilder> PathBuilderFactoryF;

		// Replace this to use a different scheduler
		public Func<PrintLayerData, IPathScheduler> SchedulerFactoryF;

        // Replace this to use a different shell selector
        public Func<PrintLayerData, ILayerShellsSelector> ShellSelectorFactoryF;

        // This is called at the beginning of each layer, you can replace to
        // implement progress bar, etc
        public Action<PrintLayerData> BeginLayerF;

		// This is called before we process each shell. The Tag is transferred
		// from the associated region in the PlanarSlice, if it had one, otherwise it is int.MaxValue
		public Action<IFillPolygon, int> BeginShellF;

		// called at the end of each layer, before we compile the paths
		public Action<PrintLayerData, PathSet> PostProcessLayerPathsF;


        // this is called on polyline paths, return *true* to filter out a path. Useful for things like very short segments, etc
        // In default Initialize(), is set to a constant multiple of tool size
        public Func<FillPolyline2d, bool> PathFilterF = null;






        protected ThreeAxisPrintGenerator()
        {
        }

        public ThreeAxisPrintGenerator(PrintMeshAssembly meshes, 
                                       PlanarSliceStack slices,
                                       SingleMaterialFFFSettings settings,
                                       ThreeAxisPrinterCompiler compiler)
        {
            Initialize(meshes, slices, settings, compiler);
        }




        public void Initialize(PrintMeshAssembly meshes, 
                               PlanarSliceStack slices,
                               SingleMaterialFFFSettings settings,
                               ThreeAxisPrinterCompiler compiler)
        {
			
            PrintMeshes = meshes;
            Slices = slices;
            Settings = settings;
            Compiler = compiler;


			// set defaults for configurable functions

			PrintLayerDataFactoryF = (layer_i, slice, settingsArg) => {
				return new PrintLayerData(layer_i, slice, settingsArg);
			};

			PathBuilderFactoryF = (layer_data) => {
				return new PathSetBuilder();
			};

			SchedulerFactoryF = get_layer_scheduler;

            ShellSelectorFactoryF = (layer_data) => {
                return new NextNearestLayerShellsSelector(layer_data.ShellFills);
            };

			BeginLayerF = (layer_data) => { };

			BeginShellF = (shell_fill, tag) => { };

			PostProcessLayerPathsF = (layer_data, paths) => { };

			if (PathFilterF == null)
				PathFilterF = (pline) => { return pline.ArcLength < 3 * Settings.Machine.NozzleDiamMM; };

        }



        public virtual bool Generate()
        {
            try {
                generate_result();
                Result = extract_result();
            } catch ( Exception e ) {
                ErrorF(e.Message, e.StackTrace);
                return false;
            }
            return true;
        }


        public virtual void GetProgress(out int curProgress, out int maxProgress)
        {
            curProgress = CurProgress;
            maxProgress = TotalProgress;
        }


        // subclasses must implement this to return GCodeFile result
        protected abstract GCodeFile extract_result();




        /*
         *  Internals
         */

 

        // tags on slice polygons get transferred to shells
        IntTagSet<IFillPolygon> ShellTags = new IntTagSet<IFillPolygon>();

        // basic progress monitoring
        int TotalProgress = 1;
        int CurProgress = 0;

        // [TODO] these should be moved to settings, or something?
        double OverhangAllowanceMM;
        protected virtual double LayerFillAngleF(int layer_i)
        {
            return (layer_i % 2 == 0) ? -45 : 45;
        }

        // start and end layers we will solve for (intersection of layercount and LayerRangeFilter)
        protected int CurStartLayer;
        protected int CurEndLayer;






        /// <summary>
        /// This is the main driver of the slicing process
        /// </summary>
        protected virtual void generate_result()
        {
            // should be parameterizable? this is 45 degrees...  (is it? 45 if nozzlediam == layerheight...)
            //double fOverhangAllowance = 0.5 * settings.NozzleDiamMM;
            OverhangAllowanceMM = Settings.LayerHeightMM / Math.Tan(45 * MathUtil.Deg2Rad);

            TotalProgress = 2 * (Slices.Count-1);
            CurProgress = 0;

            // initialize compiler and get start nozzle position
            Compiler.Begin();

            // We need N above/below shell paths to do roof/floors, and *all* shells to do support.
            // Also we can compute shells in parallel. So we just precompute them all here.
            precompute_shells();
            int nLayers = Slices.Count;

            if (Settings.EnableSupport)
                precompute_support_areas();

			PrintLayerData prevLayerData = null;

            // Now generate paths for each layer.
            // This could be parallelized to some extent, but we have to pass per-layer paths
            // to Scheduler in layer-order. Probably better to parallelize within-layer computes.
            CurStartLayer = Math.Max(0, Settings.LayerRangeFilter.a);
            CurEndLayer = Math.Min(nLayers-1, Settings.LayerRangeFilter.b);
            for ( int layer_i = CurStartLayer; layer_i <= CurEndLayer; ++layer_i ) {

				// allocate new layer data structure
				PrintLayerData layerdata = PrintLayerDataFactoryF(layer_i, Slices[layer_i], this.Settings);
				layerdata.PreviousLayer = prevLayerData;

				// create path accumulator
				PathSetBuilder pathAccum = PathBuilderFactoryF(layerdata);
				layerdata.PathAccum = pathAccum;

				// rest of code does not directly access path builder, instead it
				// sends paths to scheduler.
				IPathScheduler layerScheduler = SchedulerFactoryF(layerdata);
                GroupScheduler groupScheduler = new GroupScheduler(layerScheduler, Compiler.NozzlePosition.xy);
                //GroupScheduler groupScheduler = new PassThroughGroupScheduler(layerScheduler, Compiler.NozzlePosition.xy);
                layerdata.Scheduler = groupScheduler;

                BeginLayerF(layerdata);

				layerdata.ShellFills = get_layer_shells(layer_i);

                bool is_infill = (layer_i >= Settings.FloorLayers && layer_i < nLayers - Settings.RoofLayers - 1);

                // make path-accumulator for this layer
                pathAccum.Initialize(Compiler.NozzlePosition);
                // layer-up (ie z-change)
                pathAccum.AppendZChange(Settings.LayerHeightMM, Settings.ZTravelSpeed);

                // generate roof and floor regions. This could be done in parallel, or even pre-computed
                List<GeneralPolygon2d> roof_cover = new List<GeneralPolygon2d>();
                List<GeneralPolygon2d> floor_cover = new List<GeneralPolygon2d>();
                if (is_infill) {
                    if (Settings.RoofLayers > 0) {
                        roof_cover = find_roof_areas_for_layer(layer_i);
                    } else {
                        roof_cover = find_roof_areas_for_layer(layer_i-1);     // will return "our" layer
                    }
                    if (Settings.FloorLayers > 0) {
                        floor_cover = find_floor_areas_for_layer(layer_i);
                    } else {
                        floor_cover = find_floor_areas_for_layer(layer_i+1);   // will return "our" layer
                    }
                }

                // do support first
                // this could be done in parallel w/ roof/floor...
                if (Settings.EnableSupport) {
                    List<GeneralPolygon2d> support_areas = get_layer_support_area(layer_i);
                    groupScheduler.BeginGroup();
                    fill_support_regions(support_areas, groupScheduler, layerdata);
                    groupScheduler.EndGroup();
                }

                // selector determines what order we process shells in
                ILayerShellsSelector shellSelector = ShellSelectorFactoryF(layerdata);

                // a layer can contain multiple disjoint regions. Process each separately.
                IShellsFillPolygon shells_gen = shellSelector.Next(groupScheduler.CurrentPosition);
                while ( shells_gen != null ) { 

                    // [TODO] maybe we should schedule outermost shell after infill?
                    // schedule shell paths that we pre-computed
                    List<FillPaths2d> shells_gen_paths = shells_gen.GetFillPaths();
                    FillPaths2d outer_shell = shells_gen_paths[shells_gen_paths.Count - 1];
                    bool do_outer_last = (shells_gen_paths.Count > 1);
                    groupScheduler.BeginGroup();
                    if (do_outer_last == false) {
                        groupScheduler.AppendPaths(shells_gen_paths);
                    } else {
                        groupScheduler.AppendPaths(shells_gen_paths.GetRange(0, shells_gen_paths.Count - 1));
                    }
                    groupScheduler.EndGroup();

                    // allow client to do configuration (eg change settings for example)
                    BeginShellF(shells_gen, ShellTags.Get(shells_gen));

                    // solid fill areas are inner polygons of shell fills
                    List<GeneralPolygon2d> solid_fill_regions = shells_gen.GetInnerPolygons();

                    // if this is an infill layer, compute infill regions, and remaining solid regions
                    // (ie roof/floor regions, and maybe others)
                    List<GeneralPolygon2d> infill_regions = new List<GeneralPolygon2d>();
                    if (is_infill)
						infill_regions = make_infill_regions(layer_i, solid_fill_regions, roof_cover, floor_cover, out solid_fill_regions);
                    bool has_infill = (infill_regions.Count > 0);

                    // fill solid regions
                    groupScheduler.BeginGroup();
                    fill_solid_regions(solid_fill_regions, groupScheduler, layerdata, has_infill);
                    groupScheduler.EndGroup();

                    // fill infill regions
                    groupScheduler.BeginGroup();
                    fill_infill_regions(infill_regions, groupScheduler, layerdata);
                    groupScheduler.EndGroup();

                    groupScheduler.BeginGroup();
                    if (do_outer_last) {
                        groupScheduler.AppendPaths( new List<FillPaths2d>() { outer_shell } );
                    }
                    groupScheduler.EndGroup();

                    shells_gen = shellSelector.Next(groupScheduler.CurrentPosition);
                }

                // append open paths
                groupScheduler.BeginGroup();
                add_open_paths(layerdata, groupScheduler);
                groupScheduler.EndGroup();

                // discard the group scheduler
                layerdata.Scheduler = groupScheduler.TargetScheduler;

				// last change to post-process paths for this layer before they are baked in
				PostProcessLayerPathsF(layerdata, pathAccum.Paths);

                // compile this layer 
                // [TODO] we could do this in a separate thread, in a queue of jobs?
				Compiler.AppendPaths(pathAccum.Paths);

				// we might want to consider this layer while we process next one
				prevLayerData = layerdata;

                count_progress_step();
            }

            Compiler.End();
        }



        /// <summary>
        /// fill all infill regions
        /// </summary>
        protected virtual void fill_infill_regions(List<GeneralPolygon2d> infill_regions,
            IPathScheduler scheduler, PrintLayerData layer_data )
        {
            foreach (GeneralPolygon2d infill_poly in infill_regions) {
                List<GeneralPolygon2d> polys = new List<GeneralPolygon2d>() { infill_poly };

                if (Settings.SparseFillBorderOverlapX > 0) {
                    double offset = Settings.Machine.NozzleDiamMM * Settings.SparseFillBorderOverlapX;
                    polys = ClipperUtil.MiterOffset(polys, offset);
                }

                foreach (var poly in polys)
                    fill_infill_region(poly, scheduler, layer_data);
            }
        }


        /// <summary>
        /// fill polygon with sparse infill strategy
        /// </summary>
		protected virtual void fill_infill_region(GeneralPolygon2d infill_poly, IPathScheduler scheduler, PrintLayerData layer_data)
        {
            IPathsFillPolygon infill_gen = new SparseLinesFillPolygon(infill_poly) {
                InsetFromInputPolygon = false,
                PathSpacing = Settings.SparseLinearInfillStepX * Settings.SolidFillPathSpacingMM(),
                ToolWidth = Settings.Machine.NozzleDiamMM,
				AngleDeg = LayerFillAngleF(layer_data.layer_i)
            };
            infill_gen.Compute();

			scheduler.AppendPaths(infill_gen.GetFillPaths());
        }




        protected virtual void fill_support_regions(List<GeneralPolygon2d> support_regions,
            IPathScheduler scheduler, PrintLayerData layer_data)
        {
            foreach (GeneralPolygon2d support_poly in support_regions)
                fill_support_region(support_poly, scheduler, layer_data);
        }



        /// <summary>
        /// fill polygon with support infill strategy
        /// </summary>
		protected virtual void fill_support_region(GeneralPolygon2d support_poly, IPathScheduler scheduler, PrintLayerData layer_data)
        {
            if (support_poly.Bounds.MaxDim < 2.0)
                return;
            // useful to visualize support polygons...
            // [TODO] might make more sense to use a shell if poly is very thin/elongated, or tiny

            ShellsFillPolygon shells_gen = new ShellsFillPolygon(support_poly);
            shells_gen.PathSpacing = Settings.SolidFillPathSpacingMM();
            shells_gen.ToolWidth = Settings.Machine.NozzleDiamMM;
            shells_gen.Layers = 1;
            shells_gen.FilterSelfOverlaps = false;
            //shells_gen.FilterSelfOverlaps = true;
            //shells_gen.PreserveOuterShells = false;
            //shells_gen.SelfOverlapTolerance = Settings.SelfOverlapToleranceX * Settings.Machine.NozzleDiamMM;
            shells_gen.Compute();
            foreach (var fillpath in shells_gen.GetFillPaths())
                fillpath.SetFlags(PathTypeFlags.SupportMaterial);
            scheduler.AppendPaths(shells_gen.GetFillPaths());

            List<GeneralPolygon2d> inner_shells = shells_gen.GetInnerPolygons();
            if (Settings.SparseFillBorderOverlapX > 0) {
                double offset = Settings.Machine.NozzleDiamMM * Settings.SparseFillBorderOverlapX;
                inner_shells = ClipperUtil.MiterOffset(inner_shells, offset);
            }

            foreach ( var poly in inner_shells ) {
                if (poly.Bounds.MaxDim < 2.0)
                    continue;

                SupportLinesFillPolygon infill_gen = new SupportLinesFillPolygon(poly) {
                    InsetFromInputPolygon = false,
                    PathSpacing = Settings.SupportSpacingStepX * Settings.SolidFillPathSpacingMM(),
                    ToolWidth = Settings.Machine.NozzleDiamMM,
                    AngleDeg = 0,
                };
                infill_gen.Compute();
                foreach (var fillpath in infill_gen.GetFillPaths()) {
                    foreach (var p in fillpath.Curves)
                        Util.gDevAssert(p.TypeFlags == PathTypeFlags.SupportMaterial);
                }
                scheduler.AppendPaths(infill_gen.GetFillPaths());
            }
        }





        /// <summary>
        /// fill set of solid regions
        /// </summary>
        protected virtual void fill_solid_regions(List<GeneralPolygon2d> solid_regions,
            IPathScheduler scheduler, PrintLayerData layer_data, bool bIsInfillAdjacent)
        {
            foreach (GeneralPolygon2d solid_poly in solid_regions)
                fill_solid_region(layer_data, solid_poly, scheduler, bIsInfillAdjacent);
        }



        /// <summary>
        /// Fill polygon with solid fill strategy. 
        /// If bIsInfillAdjacent, then we optionally add one or more shells around the solid
        /// fill, to give the solid fill something to stick to (imagine dense linear fill adjacent
        /// to sparse infill area - when the extruder zigs, most of the time there is nothing
        /// for the filament to attach to, so it pulls back. ugly!)
        /// </summary>
        protected virtual void fill_solid_region(PrintLayerData layer_data, 
		                                         GeneralPolygon2d solid_poly, 
                                                 IPathScheduler scheduler,
                                                 bool bIsInfillAdjacent = false )
        {
            List<GeneralPolygon2d> fillPolys = new List<GeneralPolygon2d>() { solid_poly };

            // if we are on an infill layer, and this shell has some infill region,
            // then we are going to draw contours around solid fill so it has
            // something to stick to
            // [TODO] should only be doing this if solid-fill is adjecent to infill region.
            //   But how to determine this? not easly because we don't know which polys
            //   came from where. Would need to do loop above per-polygon
            if (bIsInfillAdjacent && Settings.InteriorSolidRegionShells > 0) {
                ShellsFillPolygon interior_shells = new ShellsFillPolygon(solid_poly);
                interior_shells.PathSpacing = Settings.SolidFillPathSpacingMM();
                interior_shells.ToolWidth = Settings.Machine.NozzleDiamMM;
                interior_shells.Layers = Settings.InteriorSolidRegionShells;
                interior_shells.InsetFromInputPolygon = false;
                interior_shells.ShellType = ShellsFillPolygon.ShellTypes.InternalShell;
                interior_shells.FilterSelfOverlaps = Settings.ClipSelfOverlaps;
                interior_shells.SelfOverlapTolerance = Settings.SelfOverlapToleranceX * Settings.Machine.NozzleDiamMM;
                interior_shells.Compute();
                scheduler.AppendPaths(interior_shells.GetFillPaths());
                fillPolys = interior_shells.InnerPolygons;
            }

            if (Settings.SolidFillBorderOverlapX > 0) {
                double offset = Settings.Machine.NozzleDiamMM * Settings.SolidFillBorderOverlapX;
                fillPolys = ClipperUtil.MiterOffset(fillPolys, offset);
            }

            // now actually fill solid regions
            foreach (GeneralPolygon2d fillPoly in fillPolys) {
				IPathsFillPolygon solid_gen = new ParallelLinesFillPolygon(fillPoly) {
                    InsetFromInputPolygon = false,
                    PathSpacing = Settings.SolidFillPathSpacingMM(),
                    ToolWidth = Settings.Machine.NozzleDiamMM,
                    AngleDeg = LayerFillAngleF(layer_data.layer_i)
                };

                solid_gen.Compute();

				scheduler.AppendPaths(solid_gen.GetFillPaths());
            }
        }



        /// <summary>
        /// Determine the sparse infill and solid fill regions for a layer, given the input regions that
        /// need to be filled, and the roof/floor areas above/below this layer. 
        /// </summary>
        protected virtual List<GeneralPolygon2d> make_infill_regions(int layer_i, 
		                                                     List<GeneralPolygon2d> fillRegions, 
                                                             List<GeneralPolygon2d> roof_cover, 
                                                             List<GeneralPolygon2d> floor_cover, 
                                                             out List<GeneralPolygon2d> solid_regions)
                                                            
        {
            List<GeneralPolygon2d> infillPolys = fillRegions;

            List<GeneralPolygon2d> roofPolys = ClipperUtil.Difference(fillRegions, roof_cover);
            List<GeneralPolygon2d> floorPolys = ClipperUtil.Difference(fillRegions, floor_cover);
            solid_regions = ClipperUtil.Union(roofPolys, floorPolys);
            if (solid_regions == null)
                solid_regions = new List<GeneralPolygon2d>();

            // [TODO] I think maybe we should actually do another set of contours for the
            // solid region. At least one. This gives the solid & infill something to
            // connect to, and gives the contours above a continuous bonding thread

            // subtract solid fill from infill regions. However because we *don't*
            // inset fill regions, we need to subtract (solid+offset), so that
            // infill won't overlap solid region
            if (solid_regions.Count > 0) {
                List<GeneralPolygon2d> solidWithBorder =
                    ClipperUtil.MiterOffset(solid_regions, Settings.Machine.NozzleDiamMM);
                infillPolys = ClipperUtil.Difference(infillPolys, solidWithBorder);
            }

            return infillPolys;
        }




        /// <summary>
        /// construct region that needs to be solid for "roofs".
        /// This is the intersection of infill polygons for the next N layers.
        /// </summary>
        protected virtual List<GeneralPolygon2d> find_roof_areas_for_layer(int layer_i)
        {
            List<GeneralPolygon2d> roof_cover = new List<GeneralPolygon2d>();

            foreach (IShellsFillPolygon shells in get_layer_shells(layer_i+1))
                roof_cover.AddRange(shells.GetInnerPolygons());

            // If we want > 1 roof layer, we need to look further ahead.
            // The full area we need to print as "roof" is the infill minus
            // the intersection of the infill areas above
            for (int k = 2; k <= Settings.RoofLayers; ++k) {
                int ri = layer_i + k;
                if (ri < LayerShells.Length) {
                    List<GeneralPolygon2d> infillN = new List<GeneralPolygon2d>();
                    foreach (IShellsFillPolygon shells in get_layer_shells(ri))
                        infillN.AddRange(shells.GetInnerPolygons());

                    roof_cover = ClipperUtil.Intersection(roof_cover, infillN);
                }
            }

            // add overhang allowance. Technically any non-vertical surface will result in
            // non-empty roof regions. However we do not need to explicitly support roofs
            // until they are "too horizontal". 
            var result = ClipperUtil.MiterOffset(roof_cover, OverhangAllowanceMM);
            return result;
        }




        /// <summary>
        /// construct region that needs to be solid for "floors"
        /// </summary>
        protected virtual List<GeneralPolygon2d> find_floor_areas_for_layer(int layer_i)
        {
            List<GeneralPolygon2d> floor_cover = new List<GeneralPolygon2d>();

            foreach (IShellsFillPolygon shells in get_layer_shells(layer_i - 1))
                floor_cover.AddRange(shells.GetInnerPolygons());

            // If we want > 1 floor layer, we need to look further back.
            for (int k = 2; k <= Settings.FloorLayers; ++k) {
                int ri = layer_i - k;
                if (ri > 0) {
                    List<GeneralPolygon2d> infillN = new List<GeneralPolygon2d>();
                    foreach (IShellsFillPolygon shells in get_layer_shells(ri))
                        infillN.AddRange(shells.GetInnerPolygons());

                    floor_cover = ClipperUtil.Intersection(floor_cover, infillN);
                }
            }

            // add overhang allowance. 
            var result = ClipperUtil.MiterOffset(floor_cover, OverhangAllowanceMM);
            return result;
        }




        /// <summary>
        /// schedule any non-polygonal paths for the given layer (eg paths
        /// that resulted from open meshes, for example)
        /// </summary>
		protected virtual void add_open_paths(PrintLayerData layerdata, IPathScheduler scheduler)
        {
			PlanarSlice slice = layerdata.Slice;
            if (slice.Paths.Count == 0)
                return;

            FillPaths2d paths = new FillPaths2d();
            for ( int pi = 0; pi < slice.Paths.Count; ++pi ) {
				FillPolyline2d pline = new FillPolyline2d(slice.Paths[pi]) {
					TypeFlags = PathTypeFlags.OpenShellPath
				};

                // leave space for end-blobs (input paths are extent we want to hit)
                pline.Trim(Settings.Machine.NozzleDiamMM / 2);

                // ignore tiny paths
                if (PathFilterF != null && PathFilterF(pline) == true)
                    continue;

                paths.Append(pline);
            }

            scheduler.AppendPaths(new List<FillPaths2d>() { paths });
        }








        // The set of perimeter fills for each layer. 
        // If we have sparse infill, we need to have multiple shells available to do roof/floors.
        // To do support, we ideally would have them all.
        // Currently we precompute all shell-fills up-front, in precompute_shells().
        // However you could override this behavior, eg do on-demand compute, in GetLayerShells()
        protected List<IShellsFillPolygon>[] LayerShells;

        /// <summary>
        /// return the set of shell-fills for a layer. This includes both the shell-fill paths
        /// and the remaining regions that need to be filled.
        /// </summary>
		protected virtual List<IShellsFillPolygon> get_layer_shells(int layer_i) {
            // evaluate shell on-demand
            //if ( LayerShells[layeri] == null ) {
            //    PlanarSlice slice = Slices[layeri];
            //    LayerShells[layeri] = compute_shells_for_slice(slice);
            //}
			return LayerShells[layer_i];
        }

        /// <summary>
        /// compute all the shells for the entire slice-stack
        /// </summary>
        protected virtual void precompute_shells()
        {
            int nLayers = Slices.Count;
            LayerShells = new List<IShellsFillPolygon>[nLayers];

            int max_roof_floor = Math.Max(Settings.RoofLayers, Settings.FloorLayers);
            int start_layer = Math.Max(0, Settings.LayerRangeFilter.a-max_roof_floor);
            int end_layer = Math.Min(nLayers - 1, Settings.LayerRangeFilter.b+max_roof_floor);

            Interval1i solve_shells = new Interval1i(start_layer, end_layer);
            gParallel.ForEach(solve_shells, (layeri) => {
                PlanarSlice slice = Slices[layeri];
                LayerShells[layeri] = compute_shells_for_slice(slice);
                count_progress_step();
            });
        }

        /// <summary>
        /// compute all the shell-fills for a given slice
        /// </summary>
        protected virtual List<IShellsFillPolygon> compute_shells_for_slice(PlanarSlice slice)
        {
            List<IShellsFillPolygon> layer_shells = new List<IShellsFillPolygon>();
            foreach (GeneralPolygon2d shape in slice.Solids) {
                IShellsFillPolygon shells_gen = compute_shells_for_shape(shape);
                layer_shells.Add(shells_gen);

                if (slice.Tags.Has(shape)) {
                    lock (ShellTags) {
                        ShellTags.Add(shells_gen, slice.Tags.Get(shape));
                    }
                }
            }
            return layer_shells;
        }

        /// <summary>
        /// compute a shell-fill for the given shape (assumption is that shape.Outer 
        /// is anoutermost perimeter)
        /// </summary>
        protected virtual IShellsFillPolygon compute_shells_for_shape(GeneralPolygon2d shape)
        {
            ShellsFillPolygon shells_gen = new ShellsFillPolygon(shape);
            shells_gen.PathSpacing = Settings.SolidFillPathSpacingMM();
            shells_gen.ToolWidth = Settings.Machine.NozzleDiamMM;
            shells_gen.Layers = Settings.Shells;
            shells_gen.FilterSelfOverlaps = Settings.ClipSelfOverlaps;
            shells_gen.SelfOverlapTolerance = Settings.SelfOverlapToleranceX * Settings.Machine.NozzleDiamMM;

            shells_gen.Compute();
            return shells_gen;
        }







        // The set of perimeter fills for each layer. 
        // If we have sparse infill, we need to have multiple shells available to do roof/floors.
        // To do support, we ideally would have them all.
        // Currently we precompute all shell-fills up-front, in precompute_shells().
        // However you could override this behavior, eg do on-demand compute, in GetLayerShells()
        protected List<GeneralPolygon2d>[] LayerSupportAreas;


        /// <summary>
        /// return the set of support-region polygons for a layer. 
        /// </summary>
		protected virtual List<GeneralPolygon2d> get_layer_support_area(int layer_i)
        {
            return LayerSupportAreas[layer_i];
        }



        /// <summary>
        /// compute support volumes for entire slice-stack
        /// </summary>
        protected virtual void precompute_support_areas()
        {
            // should be parameterizable? this is 45 degrees...
            //   (is it? 45 if nozzlediam == layerheight...)
            //double fOverhangAllowance = 0.5 * settings.NozzleDiamMM;
            double fOverhangAllowance = Settings.LayerHeightMM / Math.Tan(30 * MathUtil.Deg2Rad);
            //double fOverhangAllowance = Settings.LayerHeightMM / Math.Tan(45 * MathUtil.Deg2Rad);
            double fPrintWidth = Settings.Machine.NozzleDiamMM;
            double fMergeDownDilate = fPrintWidth * 0.5;  // see if(dilate) below
            double fSupportGapInLayer = fPrintWidth * 0.5;
            double DiscardHoleSizeMM = 0.0;

            int nLayers = Slices.Count;

            LayerSupportAreas = new List<GeneralPolygon2d>[nLayers];


            // Compute required support area for each layer
            // Note that this does *not* include thickness allowance, this is
            // the "outer" support-requiring polygon
            gParallel.ForEach(Interval1i.Range(nLayers-1), (layeri) => {
                PlanarSlice slice = Slices[layeri];
                PlanarSlice next_slice = Slices[layeri + 1];

                List<GeneralPolygon2d> insetPolys = ClipperUtil.MiterOffset(next_slice.Solids, -fOverhangAllowance);
                List<GeneralPolygon2d> supportPolys = ClipperUtil.Difference(insetPolys, slice.Solids);
                LayerSupportAreas[layeri] = supportPolys;
                count_progress_step();
            });
            LayerSupportAreas[nLayers-1] = new List<GeneralPolygon2d>();


			// now merge support layers. Process is to track "current" support area,
			// at layer below we union with that layers support, and then subtract
			// that layers solids. 
			List<GeneralPolygon2d> prevSupport = LayerSupportAreas[nLayers - 1];
            for (int i = nLayers - 2; i >= 0; --i) {
                PlanarSlice slice = Slices[i];

                // union down
                List<GeneralPolygon2d> combineSupport = null;

                // [RMS] smooth the support polygon from the previous layer. if we allow
                // shrinking then they will shrink to nothing, though...need to bound this somehow
                List<GeneralPolygon2d> support_above = new List<GeneralPolygon2d>();
                bool grow = true, shrink = false;
                foreach ( GeneralPolygon2d solid in prevSupport ) {
                    GeneralPolygon2d copy = new GeneralPolygon2d();
                    copy.Outer = new Polygon2d(solid.Outer);
                    CurveUtils2.LaplacianSmoothConstrained(copy.Outer, 0.5, 5, fMergeDownDilate, shrink, grow);
                    List<GeneralPolygon2d> outer_clip = (solid.Holes.Count == 0) ? null : ClipperUtil.ComputeOffsetPolygon(copy, -fPrintWidth, true);
                    foreach (Polygon2d hole in solid.Holes) {
                        if (hole.Bounds.MaxDim < DiscardHoleSizeMM)
                            continue;
                        Polygon2d new_hole = new Polygon2d(hole);
                        CurveUtils2.LaplacianSmoothConstrained(new_hole, 0.5, 5, fMergeDownDilate, shrink, grow);

                        List<GeneralPolygon2d> clipped_holes = 
                            ClipperUtil.Difference(new GeneralPolygon2d(new_hole), outer_clip);
                        foreach (GeneralPolygon2d cliphole in clipped_holes) {
                            new_hole = cliphole.Outer;
                            if (new_hole.Bounds.MaxDim > DiscardHoleSizeMM) {
                                if (new_hole.IsClockwise == false )
                                    new_hole.Reverse();
                                copy.AddHole(new_hole, true);
                            }
                        }
                    }
                    support_above.Add(copy);
                }



                // [TODO] should discard small interior holes here if they don't intersect layer...

                // [RMS] support polygons were contracted above, so on successive layers they will not
                // necessarily intersect (eg on angled slab, each support area will be disjoint). 
                // So, we grow them back, boolean, and shrink again
                bool dilate = true;
                if (dilate) {
                    List<GeneralPolygon2d> a = ClipperUtil.MiterOffset(support_above, fMergeDownDilate);
                    List<GeneralPolygon2d> b = ClipperUtil.MiterOffset(LayerSupportAreas[i], fMergeDownDilate);
                    combineSupport = ClipperUtil.Union(a, b);
                    combineSupport = ClipperUtil.MiterOffset(combineSupport, -fMergeDownDilate);
                } else {
                    combineSupport = ClipperUtil.Union(support_above, LayerSupportAreas[i]);
                }

                // support area we propagate down is combined area minus solid
                prevSupport = ClipperUtil.Difference(combineSupport, slice.Solids);

                // on this layer, we need to leave space for filament, by dilating solid by
                // half nozzle-width and subtracting it
                List<GeneralPolygon2d> dilatedSolid = ClipperUtil.MiterOffset(slice.Solids, fSupportGapInLayer);
                combineSupport = ClipperUtil.Difference(combineSupport, dilatedSolid);

                // the actual area we will support is nudged inwards
                //combineSupport = ClipperUtil.MiterOffset(combineSupport, -fOverhangAllowance);

                LayerSupportAreas[i] = new List<GeneralPolygon2d>();
                foreach (GeneralPolygon2d poly in combineSupport) {
                    poly.Simplify(Settings.Machine.NozzleDiamMM, Settings.Machine.NozzleDiamMM * 0.05, true);
                    LayerSupportAreas[i].Add(poly);
                }
            }

        }






        /// <summary>
        /// Factory function to return a new PathScheduler to use for this layer.
        /// </summary>
		protected virtual IPathScheduler get_layer_scheduler(PrintLayerData layer_data)
        {
			BasicPathScheduler scheduler = new BasicPathScheduler(layer_data.PathAccum, layer_data.Settings);

            // be careful on first layer
			scheduler.SpeedHint = (layer_data.layer_i == CurStartLayer) ?
                SchedulerSpeedHint.Careful : SchedulerSpeedHint.Rapid;

            return scheduler;
        }



        protected virtual void count_progress_step()
        {
            Interlocked.Increment(ref CurProgress);
        }



    }




}

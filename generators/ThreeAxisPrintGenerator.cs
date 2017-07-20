using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{

    // [TODO] flesh out this class...
    public class PrintMeshAssembly
    {
        public List<DMesh3> Meshes = new List<DMesh3>();


        public AxisAlignedBox3d TotalBounds
        {
            get {
                AxisAlignedBox3d bounds = AxisAlignedBox3d.Empty;
                foreach (var mesh in Meshes)
                    bounds.Contain(mesh.CachedBounds);
                return bounds;
            }
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

        // replace this with your own error message handler
        public Action<string, string> ErrorF = (message, stack_trace) => {
            System.Console.WriteLine("[EXCEPTION] ThreeAxisPrintGenerator: " + message + "\nSTACK TRACE: " + stack_trace);
        };

        // This is called at the beginning of each layer, you can replace to
        // implement progress bar, etc
        public Action<int> BeginLayerF = (layeri) => { };

        // This is called before we process each shell. The Tag is transferred
        // from the associated region in the PlanarSlice, if it had one, otherwise it is int.MaxValue
        public Action<IFillPolygon, int> BeginShellF = (shell_fill, tag) => { };


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



        public virtual void Initialize(PrintMeshAssembly meshes, 
                               PlanarSliceStack slices,
                               SingleMaterialFFFSettings settings,
                               ThreeAxisPrinterCompiler compiler)
        {
            PrintMeshes = meshes;
            Slices = slices;
            Settings = settings;
            Compiler = compiler;
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



        // subclasses must implement this to return GCodeFile result
        protected abstract GCodeFile extract_result();




        /*
         *  Internals
         */


        List<ShellsFillPolygon>[] LayerShells;

        // tags on slice polygons get transferred to shells
        IntTagSet<IFillPolygon> ShellTags = new IntTagSet<IFillPolygon>();

        // [TODO] these should be moved to settings, or something?
        double OverhangAllowanceMM;
        protected virtual double LayerFillAngleF(int layer_i)
        {
            return (layer_i % 2 == 0) ? -45 : 45;
        }



        /// <summary>
        /// This is the main driver of the slicing process
        /// </summary>
        protected virtual void generate_result()
        {

            // should be parameterizable? this is 45 degrees...  (is it? 45 if nozzlediam == layerheight...)
            //double fOverhangAllowance = 0.5 * settings.NozzleDiamMM;
            OverhangAllowanceMM = Settings.LayerHeightMM / Math.Tan(45 * MathUtil.Deg2Rad);


            // initialize compiler and get start nozzle position
            Compiler.Begin();

            // We need N above/below shell paths to do roof/floors, and *all* shells to do support.
            // Also we can compute shells in parallel. So we just precompute them all here.
            precompute_shells();
            int nLayers = Slices.Count;

            // Now generate paths for each layer.
            // This could be parallelized to some extent, but we have to pass per-layer paths
            // to Scheduler in layer-order. Probably better to parallelize within-layer computes.
            for ( int layer_i = 0; layer_i < nLayers; ++layer_i ) {
                BeginLayerF(layer_i);

                bool is_infill = (layer_i >= Settings.FloorLayers && layer_i < nLayers - Settings.RoofLayers - 1);

                // make path-accumulator for this layer
                PathSetBuilder paths = new PathSetBuilder();
                paths.Initialize(Compiler.NozzlePosition);
                // layer-up (ie z-change)
                paths.AppendZChange(Settings.LayerHeightMM, Settings.ZTravelSpeed);

                // rest of code does not directly access path builder, instead if
                // sends paths to scheduler.
                PathScheduler scheduler = new PathScheduler(paths, Settings);

                // generate roof and floor regions. This could be done in parallel, or even pre-computed
                List<GeneralPolygon2d> roof_cover = new List<GeneralPolygon2d>();
                List<GeneralPolygon2d> floor_cover = new List<GeneralPolygon2d>();
                if (is_infill) {
                    roof_cover = make_roof(layer_i);
                    floor_cover = make_floor(layer_i);
                }

                // a layer can contain multiple disjoint regions. Process each separately.
                List<ShellsFillPolygon> layer_shells = LayerShells[layer_i];
                for (int si = 0; si < layer_shells.Count; si++) {

                    // schedule shell paths that we pre-computed
                    ShellsFillPolygon shells_gen = layer_shells[si];
                    scheduler.AppendPaths(shells_gen.Shells);

                    // all client to do configuration (eg change settings for example)
                    BeginShellF(shells_gen, ShellTags.Get(shells_gen));

                    // solid fill areas are inner polygons of shell fills
                    List<GeneralPolygon2d> solid_fill_regions = shells_gen.InnerPolygons;

                    // if this is an infill layer, compute infill regions, and remaining solid regions
                    // (ie roof/floor regions, and maybe others)
                    List<GeneralPolygon2d> infill_regions = new List<GeneralPolygon2d>();
                    if (is_infill)
                        infill_regions = make_infill(layer_i, solid_fill_regions, roof_cover, floor_cover, out solid_fill_regions);
                    bool has_infill = (infill_regions.Count > 0);

                    // fill solid regions
                    foreach (GeneralPolygon2d solid_poly in solid_fill_regions) 
                        fill_solid_region(layer_i, solid_poly, scheduler, has_infill);

                    // fill infill regions
                    foreach (GeneralPolygon2d infill_poly in infill_regions) 
                        fill_infill_region(layer_i, infill_poly, scheduler);
                }

                // resulting paths for this layer (Currently we are just discarding this after compiling)
                PathSet layerPaths = paths.Paths;

                // compile this layer
                Compiler.AppendPaths(layerPaths);
            }

            Compiler.End();
        }




        /// <summary>
        /// fill polygon with sparse infill strategy
        /// </summary>
        protected virtual void fill_infill_region(int layer_i, GeneralPolygon2d infill_poly, IPathScheduler scheduler)
        {
            DenseLinesFillPolygon infill_gen = new DenseLinesFillPolygon(infill_poly) {
                InsetFromInputPolygon = false,
                PathSpacing = Settings.SparseLinearInfillStepX * Settings.FillPathSpacingMM,
                ToolWidth = Settings.NozzleDiamMM,
                AngleDeg = LayerFillAngleF(layer_i)
            };
            infill_gen.Compute();
            scheduler.AppendPaths(infill_gen.Paths);
        }



        /// <summary>
        /// Fill polygon with solid fill strategy. 
        /// If bIsInfillAdjacent, then we optionally add one or more shells around the solid
        /// fill, to give the solid fill something to stick to (imagine dense linear fill adjacent
        /// to sparse infill area - when the extruder zigs, most of the time there is nothing
        /// for the filament to attach to, so it pulls back. ugly!)
        /// </summary>
        protected virtual void fill_solid_region(int layer_i, GeneralPolygon2d solid_poly, 
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
                interior_shells.PathSpacing = Settings.FillPathSpacingMM;
                interior_shells.ToolWidth = Settings.NozzleDiamMM;
                interior_shells.Layers = Settings.InteriorSolidRegionShells;
                interior_shells.InsetFromInputPolygon = false;
                interior_shells.Compute();
                scheduler.AppendShells(interior_shells.Shells);
                fillPolys = interior_shells.InnerPolygons;
            }

            // now actually fill solid regions
            foreach (GeneralPolygon2d fillPoly in fillPolys) {
                DenseLinesFillPolygon solid_gen = new DenseLinesFillPolygon(fillPoly) {
                    InsetFromInputPolygon = false,
                    PathSpacing = Settings.FillPathSpacingMM,
                    ToolWidth = Settings.NozzleDiamMM,
                    AngleDeg = LayerFillAngleF(layer_i)
                };
                solid_gen.Compute();
                scheduler.AppendPaths(solid_gen.Paths);
            }
        }



        /// <summary>
        /// Determine the sparse infill and solid fill regions for a layer, given the input regions that
        /// need to be filled, and the roof/floor areas above/below this layer. 
        /// </summary>
        protected virtual List<GeneralPolygon2d> make_infill(int layer_i, List<GeneralPolygon2d> fillRegions, 
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
                    ClipperUtil.MiterOffset(solid_regions, Settings.NozzleDiamMM);
                infillPolys = ClipperUtil.Difference(infillPolys, solidWithBorder);
            }

            return infillPolys;
        }




        /// <summary>
        /// construct region that needs to be solid for "roofs".
        /// This is the intersection of infill polygons for the next N layers.
        /// </summary>
        protected virtual List<GeneralPolygon2d> make_roof(int layer_i)
        {
            List<GeneralPolygon2d> roof_cover = new List<GeneralPolygon2d>();

            foreach (ShellsFillPolygon shells in LayerShells[layer_i + 1])
                roof_cover.AddRange(shells.InnerPolygons);

            // If we want > 1 roof layer, we need to look further ahead.
            // The full area we need to print as "roof" is the infill minus
            // the intersection of the infill areas above
            for (int k = 2; k <= Settings.RoofLayers; ++k) {
                int ri = layer_i + k;
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
            var result = ClipperUtil.MiterOffset(roof_cover, OverhangAllowanceMM);
            return result;
        }




        /// <summary>
        /// construct region that needs to be solid for "floors"
        /// </summary>
        protected virtual List<GeneralPolygon2d> make_floor(int layer_i)
        {
            List<GeneralPolygon2d> floor_cover = new List<GeneralPolygon2d>();

            foreach (ShellsFillPolygon shells in LayerShells[layer_i - 1])
                floor_cover.AddRange(shells.InnerPolygons);

            // If we want > 1 floor layer, we need to look further back.
            for (int k = 2; k <= Settings.FloorLayers; ++k) {
                int ri = layer_i - k;
                if (ri > 0) {
                    List<GeneralPolygon2d> infillN = new List<GeneralPolygon2d>();
                    foreach (ShellsFillPolygon shells in LayerShells[ri])
                        infillN.AddRange(shells.InnerPolygons);

                    floor_cover = ClipperUtil.Intersection(floor_cover, infillN);
                }
            }

            // add overhang allowance. 
            var result = ClipperUtil.MiterOffset(floor_cover, OverhangAllowanceMM);
            return result;
        }






        protected virtual void precompute_shells()
        {
            int nLayers = Slices.Count;

            LayerShells = new List<ShellsFillPolygon>[nLayers];
            gParallel.ForEach(Interval1i.Range(nLayers), (layeri) => {
                PlanarSlice slice = Slices[layeri];
                LayerShells[layeri] = new List<ShellsFillPolygon>();

                List<GeneralPolygon2d> solids = slice.Solids;

                foreach (GeneralPolygon2d shape in solids) {
                    ShellsFillPolygon shells_gen = new ShellsFillPolygon(shape);
                    shells_gen.PathSpacing = Settings.FillPathSpacingMM;
                    shells_gen.ToolWidth = Settings.NozzleDiamMM;
                    shells_gen.Layers = Settings.Shells;
                    shells_gen.Compute();
                    LayerShells[layeri].Add(shells_gen);

                    if (slice.Tags.Has(shape))
                        ShellTags.Add(shells_gen, slice.Tags.Get(shape));
                }
            });
        }




    }




}

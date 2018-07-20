using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using g3;

namespace gs
{
    /// <summary>
    /// Computes a PlanarSliceStack from a set of input meshes, by horizonally
    /// slicing them at regular Z-intervals. This is where we need to sort out
    /// any complications like overlapping shells, etc. Much of that work is
    /// done in PlanarSlice.resolve().
    /// 
    /// The input meshes are not modified in this process.
    /// </summary>
	public class MeshPlanarMillSlicer : BaseSlicer
    {



        // factory functions you can replace to customize objects/behavior
        public Func<PlanarSliceStack> SliceStackFactoryF = () => { return new PlanarSliceStack(); };
        public Func<Interval1d, double, int, PlanarSlice> SliceFactoryF = (ZSpan, ZHeight, idx) => {
            return new PlanarSlice() {  LayerZSpan = ZSpan, Z = ZHeight, LayerIndex = idx };
        };


        /// <summary>
        /// Default Slice height
        /// </summary>
		public double LayerHeightMM = 0.2;


        /// <summary>
        /// Min tool-size, we use this to ignore planar Z regions that are smaller than tool
        /// </summary>
		public double ToolDiameter = 6.35;


        /// <summary>
        /// Amount we grow the stock volume at each layer
        /// </summary>
        public double ExpandStockAmount = 0;

        /// <summary>
        /// provide this function to override default LayerHeighMM
        /// </summary>
        public Func<int, double> LayerHeightF = null;

        /// <summary>
        /// Open-sheet meshes slice into open paths. For OpenPathsModes.Embedded mode, we need
        /// to subtract thickened path from the solids. This is the path thickness.
        /// </summary>
        public double OpenPathDefaultWidthMM = 0.4;


        /// <summary>
        /// Normally we slice in interval [zmin,zmax]. Set this to 0 if you
        /// want to slice [0,zmax].
        /// </summary>
        public double SetMinZValue = double.MinValue;

        /// <summary>
        /// If true, then any empty slices at bottom of stack are discarded.
        /// </summary>
        public bool DiscardEmptyBaseSlices = false;


		public enum SliceLocations {
			Base, EpsilonBase
		}

        /// <summary>
        /// Where in layer should we compute slice
        /// </summary>
		public SliceLocations SliceLocation = SliceLocations.EpsilonBase;

        /// <summary>
        /// How should open paths be handled. Is overriden by
        /// PrintMeshOptions.OpenPathsModes for specific meshes
        /// </summary>
        public PrintMeshOptions.OpenPathsModes DefaultOpenPathMode = PrintMeshOptions.OpenPathsModes.Clipped;


        /// <summary>
        /// If this is set, all incoming polygons are clipped against it
        /// </summary>
        public List<GeneralPolygon2d> ValidRegions = null;


        public int MaxLayerCount = 10000;		// just for sanity-check



		public MeshPlanarMillSlicer()
		{
		}


        public int AddMesh(DMesh3 mesh, PrintMeshOptions options) {
            SliceMesh m = new SliceMesh() {
                mesh = mesh,
                bounds = mesh.CachedBounds,
                options = options
            };
            int idx = Meshes.Count;
            Meshes.Add(m);
            return idx;
		}
        public int AddMesh(DMesh3 mesh) {
            return AddMesh(mesh, PrintMeshOptions.Default());
        }


        public bool Add(PrintMeshAssembly assy)
        {
            foreach ( var pair in assy.MeshesAndOptions()) 
                AddMesh(pair.Item1, pair.Item2);
            return true;
        }




        public class Result
        {
            public PlanarSliceStack Clearing;
            public PlanarSliceStack HorizontalFinish;

            public double TopZ;
            public double BaseZ;

            public Result()
            {
                Clearing = new PlanarSliceStack();
                HorizontalFinish = new PlanarSliceStack();
            }
        }



        /// <summary>
        /// Slice the meshes and return the slice stack. 
        /// </summary>
		public Result Compute()
		{
            Result result = new Result();
            if (Meshes.Count == 0)
                return result;

            // find Z interval we want to slice in
			Interval1d zrange = Interval1d.Empty;
			foreach ( var meshinfo in Meshes ) {
				zrange.Contain(meshinfo.bounds.Min.z);
				zrange.Contain(meshinfo.bounds.Max.z);
			}
            if (SetMinZValue != double.MinValue)
                zrange.a = SetMinZValue;

            result.TopZ = Math.Round(zrange.b, PrecisionDigits);
            result.BaseZ = Math.Round(zrange.a, PrecisionDigits);

            // [TODO] might be able to make better decisions if we took flat regions
            // into account when constructing initial Z-heights? if we have large flat
            // region just below Zstep, might make sense to do two smaller Z-steps so we
            // can exactly hit it??

            // construct list of clearing Z-heights
            List<double> clearingZLayers = new List<double>();
            double cur_layer_z = zrange.b;
            int layer_i = 0;
            while (cur_layer_z > zrange.a) {
                double layer_height = get_layer_height(layer_i);
                cur_layer_z -= layer_height;
                double z = Math.Round(cur_layer_z, PrecisionDigits);
                clearingZLayers.Add(z);
                layer_i++;
            }
            if ( clearingZLayers.Last() < result.BaseZ )
                clearingZLayers[clearingZLayers.Count-1] = result.BaseZ;
            if ( clearingZLayers.Last() == clearingZLayers[clearingZLayers.Count-2] )
                clearingZLayers.RemoveAt(clearingZLayers.Count-1);

            // construct layer slices from Z-heights
            List<PlanarSlice> clearing_slice_list = new List<PlanarSlice>();
            layer_i = 0;
            for ( int i = 0; i < clearingZLayers.Count; ++i ) { 
                double layer_height = (i == clearingZLayers.Count-1) ? 
                    (result.TopZ-clearingZLayers[i]) : (clearingZLayers[i+1]-clearingZLayers[i]);
                double z = clearingZLayers[i];
                Interval1d zspan = new Interval1d(z, z+layer_height);

                if (SliceLocation == SliceLocations.EpsilonBase) 
                    z += 0.001;

                PlanarSlice slice = SliceFactoryF(zspan, z, layer_i);
                clearing_slice_list.Add(slice);
                layer_i++;
			}


			int NH = clearing_slice_list.Count;
            if (NH > MaxLayerCount)
                throw new Exception("MeshPlanarSlicer.Compute: exceeded layer limit. Increase .MaxLayerCount.");

            PlanarSlice[] clearing_slices = clearing_slice_list.ToArray();

            // assume Resolve() takes 2x as long as meshes...
            TotalCompute = (Meshes.Count * NH) +  (2*NH);
            Progress = 0;

            // compute slices separately for each mesh
            for (int mi = 0; mi < Meshes.Count; ++mi ) {
                if (Cancelled())
                    break;

				DMesh3 mesh = Meshes[mi].mesh;
                PrintMeshOptions mesh_options = Meshes[mi].options;

                // [TODO] should we hang on to this spatial? or should it be part of assembly?
                DMeshAABBTree3 spatial = new DMeshAABBTree3(mesh, true);
				AxisAlignedBox3d bounds = Meshes[mi].bounds;

                bool is_cavity = mesh_options.IsCavity;
                bool is_crop = mesh_options.IsCropRegion;
                bool is_support = mesh_options.IsSupport;
                bool is_closed = (mesh_options.IsOpen) ? false : mesh.IsClosed();
                var useOpenMode = (mesh_options.OpenPathMode == PrintMeshOptions.OpenPathsModes.Default) ?
                    DefaultOpenPathMode : mesh_options.OpenPathMode;

                if (is_crop || is_support)
                    throw new Exception("Not supported!");

                // each layer is independent so we can do in parallel
                gParallel.ForEach(Interval1i.Range(NH), (i) => {
                    if (Cancelled())
                        return;

                    double z = clearing_slices[i].Z;
					if (z < bounds.Min.z || z > bounds.Max.z)
						return;

                    // compute cut
                    Polygon2d[] polys; PolyLine2d[] paths;
                    ComputeSlicePlaneCurves(mesh, spatial, z, is_closed, out polys, out paths);

                    if (is_closed) {
						// construct planar complex and "solids"
						// (ie outer polys and nested holes)
						PlanarComplex complex = new PlanarComplex();
						foreach (Polygon2d poly in polys)
							complex.Add(poly);

						PlanarComplex.FindSolidsOptions options
									 = PlanarComplex.FindSolidsOptions.Default;
						options.WantCurveSolids = false;
						options.SimplifyDeviationTolerance = 0.001;
						options.TrustOrientations = true;
						options.AllowOverlappingHoles = true;

						PlanarComplex.SolidRegionInfo solids = complex.FindSolidRegions(options);
                        List<GeneralPolygon2d> solid_polygons = ApplyValidRegions(solids.Polygons);

                        if (is_cavity) {
                            add_cavity_polygons(clearing_slices[i], solid_polygons, mesh_options);
                        } else {
                            if (ExpandStockAmount > 0)
                                solid_polygons = ClipperUtil.MiterOffset(solid_polygons, ExpandStockAmount);
                            add_solid_polygons(clearing_slices[i], solid_polygons, mesh_options);
                        }
                    } 

                    Interlocked.Increment(ref Progress);
				});  // end of parallel.foreach
				              
			} // end mesh iter

            // resolve planar intersections, etc
            gParallel.ForEach(Interval1i.Range(NH), (i) => {
                if (Cancelled())
                    return;
                clearing_slices[i].Resolve();
                Interlocked.Add(ref Progress, 2);
            });
            // add to clearing stack
            result.Clearing = SliceStackFactoryF();
            for (int k = 0; k < clearing_slices.Length; ++k)
                result.Clearing.Add(clearing_slices[k]);



            /*
             * Horizontal planar regions finishing pass. 
             * First we find all planar horizontal Z-regions big enough to mill.
             * Then we add slices at the Z's we haven't touched yet.
             * 
             * Cannot just 'fill' planar regions because we will miss edges that might
             * be millable. So we grow region and then intersect with full-slice millable area.
             */

            // find set of horizontal flat regions
            Dictionary<double, List<PlanarRegion>> flat_regions = FindPlanarZRegions(ToolDiameter);
            if (flat_regions.Count == 0)
                goto done_slicing;

            // if we have already milled this exact Z-height in clearing pass, then we can skip it
            List<double> doneZ = new List<double>();
            foreach (double z in flat_regions.Keys) {
                if (clearingZLayers.Contains(z))
                    doneZ.Add(z);
            }
            foreach (var z in doneZ)
                flat_regions.Remove(z);

            // create slice for each layer
            PlanarSlice[] horz_slices = new PlanarSlice[flat_regions.Count];
            List<double> flatZ = new List<double>(flat_regions.Keys);
            flatZ.Sort();
            for ( int k = 0; k < horz_slices.Length; ++k) {
                double z = flatZ[k];
                Interval1d zspan = new Interval1d(z, z + LayerHeightMM);
                horz_slices[k] = SliceFactoryF(zspan, z, k);

                // compute full millable region slightly above this slice.
                PlanarSlice clip_slice = ComputeSolidSliceAtZ(z + 0.0001, false);
                clip_slice.Resolve();

                // extract planar polys
                List<Polygon2d> polys = GetPlanarPolys(flat_regions[z]);
                PlanarComplex complex = new PlanarComplex();
                foreach (Polygon2d poly in polys)
                    complex.Add(poly);

                // convert to planar solids
                PlanarComplex.FindSolidsOptions options
                             = PlanarComplex.FindSolidsOptions.SortPolygons;
                options.SimplifyDeviationTolerance = 0.001;
                options.TrustOrientations = true;
                options.AllowOverlappingHoles = true;
                PlanarComplex.SolidRegionInfo solids = complex.FindSolidRegions(options);
                List<GeneralPolygon2d> solid_polygons = ApplyValidRegions(solids.Polygons);

                // If planar solid has holes, then when we do inset later, we might lose
                // too-thin parts. Shrink the holes to avoid this case. 
                //FilterHoles(solid_polygons, 0.55 * ToolDiameter);

                // ok now we need to expand region and intersect with full region.
                solid_polygons = ClipperUtil.MiterOffset(solid_polygons, ToolDiameter*0.5, 0.0001);
                solid_polygons = ClipperUtil.Intersection(solid_polygons, clip_slice.Solids, 0.0001);

                // Same idea as above, but if we do after, we keep more of the hole and
                // hence do less extra clearing. 
                // Also this could then be done at the slicer level instead of here...
                // (possibly this entire thing should be done at slicer level, except we need clip_slice!)
                FilterHoles(solid_polygons, 1.1 * ToolDiameter);

                add_solid_polygons(horz_slices[k], solid_polygons, PrintMeshOptions.Default());
            }

            // resolve planar intersections, etc
            int NF = horz_slices.Length;
            gParallel.ForEach(Interval1i.Range(NF), (i) => {
                if (Cancelled())
                    return;
                horz_slices[i].Resolve();
                Interlocked.Add(ref Progress, 2);
            });
            // add to clearing stack
            result.HorizontalFinish = SliceStackFactoryF();
            for (int k = 0; k < horz_slices.Length; ++k)
                result.HorizontalFinish.Add(horz_slices[k]);

            done_slicing:
            return result;
		}



        /// <summary>
        /// Shrink holes inside polys such that if we do offset/2, the hole and
        /// outer offsets will (probably) not collide. Two options:
        ///   1) contract and then dilate each hole. This doesn't handle long skinny holes?
        ///   2) shrink outer by offset and then intersect with holes
        /// Currently using (2). This is better, right?
        /// </summary>
        protected void FilterHoles(List<GeneralPolygon2d> polys, double offset)
        {
            foreach ( var poly in polys ) {
                if (poly.Holes.Count == 0)
                    continue;

                List<GeneralPolygon2d> outer_inset = ClipperUtil.MiterOffset(
                    new GeneralPolygon2d(poly.Outer), -offset);

                List<GeneralPolygon2d> hole_polys = new List<GeneralPolygon2d>();
                foreach (var hole in poly.Holes) {
                    hole.Reverse();
                    hole_polys.Add(new GeneralPolygon2d(hole));
                }

                //List<GeneralPolygon2d> contracted = ClipperUtil.MiterOffset(hole_polys, -offset, 0.01);
                //List<GeneralPolygon2d> dilated = ClipperUtil.MiterOffset(hole_polys, offset, 0.01);

                List<GeneralPolygon2d> dilated = ClipperUtil.Intersection(hole_polys, outer_inset, 0.01);

                poly.ClearHoles();

                List<Polygon2d> new_holes = new List<Polygon2d>();
                foreach (var dpoly in dilated) {
                    dpoly.Outer.Reverse();
                    poly.AddHole(dpoly.Outer, false, false);
                }
            }
        }




        protected PlanarSlice ComputeSolidSliceAtZ(double z, bool bCavitySolids)
        {
            PlanarSlice temp = new PlanarSlice();

            for (int mi = 0; mi < Meshes.Count; ++mi) {
                if (Cancelled())
                    break;

                DMesh3 mesh = Meshes[mi].mesh;
                PrintMeshOptions mesh_options = Meshes[mi].options;

                // [TODO] should we hang on to this spatial? or should it be part of assembly?
                DMeshAABBTree3 spatial = new DMeshAABBTree3(mesh, true);
                AxisAlignedBox3d bounds = Meshes[mi].bounds;

                bool is_cavity = mesh_options.IsCavity;
                bool is_crop = mesh_options.IsCropRegion;
                bool is_support = mesh_options.IsSupport;
                bool is_closed = (mesh_options.IsOpen) ? false : mesh.IsClosed();
                var useOpenMode = (mesh_options.OpenPathMode == PrintMeshOptions.OpenPathsModes.Default) ?
                    DefaultOpenPathMode : mesh_options.OpenPathMode;
                if (is_crop || is_support)
                    throw new Exception("Not supported!");
                if (bCavitySolids && is_cavity == false)
                    continue;

                // compute cut
                Polygon2d[] polys; PolyLine2d[] paths;
                ComputeSlicePlaneCurves(mesh, spatial, z, is_closed, out polys, out paths);

                if (is_closed) {
                    // construct planar complex and "solids"
                    // (ie outer polys and nested holes)
                    PlanarComplex complex = new PlanarComplex();
                    foreach (Polygon2d poly in polys)
                        complex.Add(poly);

                    PlanarComplex.FindSolidsOptions options
                                    = PlanarComplex.FindSolidsOptions.Default;
                    options.WantCurveSolids = false;
                    options.SimplifyDeviationTolerance = 0.001;
                    options.TrustOrientations = true;
                    options.AllowOverlappingHoles = true;

                    PlanarComplex.SolidRegionInfo solids = complex.FindSolidRegions(options);
                    List<GeneralPolygon2d> solid_polygons = ApplyValidRegions(solids.Polygons);

                    if (is_cavity && bCavitySolids == false)
                        add_cavity_polygons(temp, solid_polygons, mesh_options);
                    else
                        add_solid_polygons(temp, solid_polygons, mesh_options);
                }
            }

            return temp;
        }





        protected virtual double get_layer_height(int layer_i)
        {
            return (LayerHeightF != null) ? LayerHeightF(layer_i) : LayerHeightMM;
        }

        protected virtual void add_cavity_polygons(PlanarSlice slice, List<GeneralPolygon2d> polygons, PrintMeshOptions options)
        {
            slice.AddCavityPolygons(polygons);
        }

        protected virtual void add_solid_polygons(PlanarSlice slice, List<GeneralPolygon2d> polygons, PrintMeshOptions options)
        {
            slice.AddPolygons(polygons);
        }



        protected virtual List<GeneralPolygon2d> ApplyValidRegions(List<GeneralPolygon2d> polygonsIn)
        {
            if (ValidRegions == null || ValidRegions.Count == 0)
                return polygonsIn;
            return ClipperUtil.Intersection(polygonsIn, ValidRegions);
        }

        protected virtual List<PolyLine2d> ApplyValidRegions(List<PolyLine2d> plinesIn)
        {
            if (ValidRegions == null || ValidRegions.Count == 0)
                return plinesIn;
            List<PolyLine2d> clipped = new List<PolyLine2d>();
            foreach (var pline in plinesIn)
                clipped.AddRange(ClipperUtil.ClipAgainstPolygon(ValidRegions, pline, true));
            return clipped;
        }





	}
}

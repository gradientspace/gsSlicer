using System;
using System.Collections.Generic;
using System.Threading;
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
	public class MeshPlanarSlicer
	{
        class SliceMesh
        {
            public DMesh3 mesh;
            public AxisAlignedBox3d bounds;

            public PrintMeshOptions options;
        }
        List<SliceMesh> Meshes = new List<SliceMesh>();


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
        /// provide this function to override default LayerHeighMM
        /// </summary>
        public Func<int, double> LayerHeightF = null;

        /// <summary>
        /// Open-sheet meshes slice into open paths. For OpenPathsModes.Embedded mode, we need
        /// to subtract thickened path from the solids. This is the path thickness.
        /// </summary>
        public double OpenPathDefaultWidthMM = 0.4;


		/// <summary>
		/// Support "tips" (ie z-minima vertices) can be detected geometrically and
		/// added to PlanarSlice.InputSupportPoints. 
		/// </summary>
		public bool SupportMinZTips = false;

		/// <summary>
		/// What is the largest floating polygon we will consider a "tip"
		/// </summary>
		public double MinZTipMaxDiam = 2.0;

		/// <summary>
		/// Often desirable to support a Z-minima tip several layers "up" around it.
		/// This is how many layers.
		/// </summary>
		public int MinZTipExtraLayers = 6;


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
			Base, EpsilonBase, MidLine
		}

        /// <summary>
        /// Where in layer should we compute slice
        /// </summary>
		public SliceLocations SliceLocation = SliceLocations.MidLine;

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


        // these can be used for progress tracking
        public int TotalCompute = 0;
        public int Progress = 0;


        public Func<bool> CancelF = () => { return false; };
        public bool WasCancelled = false;


		public MeshPlanarSlicer()
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



        /// <summary>
        /// Slice the meshes and return the slice stack. 
        /// </summary>
		public PlanarSliceStack Compute()
		{
            if (Meshes.Count == 0)
                return new PlanarSliceStack();

			Interval1d zrange = Interval1d.Empty;
			foreach ( var meshinfo in Meshes ) {
				zrange.Contain(meshinfo.bounds.Min.z);
				zrange.Contain(meshinfo.bounds.Max.z);
			}
            if (SetMinZValue != double.MinValue)
                zrange.a = SetMinZValue;


            // construct layers
            List<PlanarSlice> slice_list = new List<PlanarSlice>();

            double cur_layer_z = zrange.a;
            int layer_i = 0;
            while ( cur_layer_z < zrange.b ) { 
                double layer_height = get_layer_height(layer_i);
                double z = cur_layer_z;
                Interval1d zspan = new Interval1d(z, z + layer_height);
				if (SliceLocation == SliceLocations.EpsilonBase)
					z += 0.01 * layer_height;
				else if (SliceLocation == SliceLocations.MidLine)
					z += 0.5 * layer_height;

                PlanarSlice slice = SliceFactoryF(zspan, z, layer_i);
                slice.EmbeddedPathWidth = OpenPathDefaultWidthMM;
                slice_list.Add(slice);

                layer_i++;
                cur_layer_z += layer_height;
			}
			int NH = slice_list.Count;
            if (NH > MaxLayerCount)
                throw new Exception("MeshPlanarSlicer.Compute: exceeded layer limit. Increase .MaxLayerCount.");

            PlanarSlice[] slices = slice_list.ToArray();

            // determine if we have crop objects
            bool have_crop_objects = false;
            foreach (var mesh in Meshes) {
                if (mesh.options.IsCropRegion)
                    have_crop_objects = true;
            }


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

                // each layer is independent so we can do in parallel
                gParallel.ForEach(Interval1i.Range(NH), (i) => {
                    if (Cancelled())
                        return;

                    double z = slices[i].Z;
					if (z < bounds.Min.z || z > bounds.Max.z)
						return;

                    // compute cut
                    Polygon2d[] polys; PolyLine2d[] paths;
                    compute_plane_curves(mesh, spatial, z, is_closed, out polys, out paths);

                    // if we didn't hit anything, try again with jittered plane
                    // [TODO] this could be better...
                    if ( (is_closed && polys.Length == 0) || (is_closed == false &&  polys.Length == 0 && paths.Length == 0)) {
                        double jitterz = slices[i].LayerZSpan.Interpolate(0.75);
                        compute_plane_curves(mesh, spatial, jitterz, is_closed, out polys, out paths);
                    }

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

                        if (is_support)
                            add_support_polygons(slices[i], solid_polygons, mesh_options);
                        else if (is_cavity)
                            add_cavity_polygons(slices[i], solid_polygons, mesh_options);
                        else if (is_crop)
                            add_crop_region_polygons(slices[i], solid_polygons, mesh_options);
                        else
                            add_solid_polygons(slices[i], solid_polygons, mesh_options);

                    } else if (useOpenMode != PrintMeshOptions.OpenPathsModes.Ignored) {

                        // [TODO] 
                        //   - does not really handle clipped polygons properly, there will be an extra break somewhere...
                        List<PolyLine2d> all_paths = new List<PolyLine2d>(paths);
                        foreach (Polygon2d poly in polys)
                            all_paths.Add(new PolyLine2d(poly, true));

                        List<PolyLine2d> open_polylines = ApplyValidRegions(all_paths);
                        foreach (PolyLine2d pline in open_polylines) {
                            if (useOpenMode == PrintMeshOptions.OpenPathsModes.Embedded )
                                slices[i].AddEmbeddedPath(pline);   
                            else
                                slices[i].AddClippedPath(pline);
                        }

                    }

                    Interlocked.Increment(ref Progress);
				});  // end of parallel.foreach
				              
			} // end mesh iter

            // resolve planar intersections, etc
            gParallel.ForEach(Interval1i.Range(NH), (i) => {
                if (Cancelled())
                    return;

                if (have_crop_objects && slices[i].InputCropRegions.Count == 0) {
                    // don't resolve, we have fully cropped this layer
                } else {
                    slices[i].Resolve();
                }

                Interlocked.Add(ref Progress, 2);
            });

            // discard spurious empty slices
            int last = slices.Length-1;
            while (slices[last].IsEmpty && last > 0)
                last--;
            int first = 0;
            if (DiscardEmptyBaseSlices || have_crop_objects) {
                while (slices[first].IsEmpty && first < slices.Length)
                    first++;
            }

            PlanarSliceStack stack = SliceStackFactoryF();
            for (int k = first; k <= last; ++k)
                stack.Add(slices[k]);

			if ( SupportMinZTips )
				stack.AddMinZTipSupportPoints(MinZTipMaxDiam, MinZTipExtraLayers);

			return stack;
		}


        protected virtual bool Cancelled()
        {
            if (WasCancelled)
                return true;
            bool cancel = CancelF();
            if (cancel) {
                WasCancelled = true;
                return true;
            }
            return false;
        }



        protected virtual double get_layer_height(int layer_i)
        {
            return (LayerHeightF != null) ? LayerHeightF(layer_i) : LayerHeightMM;
        }


        protected virtual void add_support_polygons(PlanarSlice slice, List<GeneralPolygon2d> polygons, PrintMeshOptions options)
        {
            slice.AddSupportPolygons(polygons);
        }

        protected virtual void add_cavity_polygons(PlanarSlice slice, List<GeneralPolygon2d> polygons, PrintMeshOptions options)
        {
            slice.AddCavityPolygons(polygons);
        }

        protected virtual void add_crop_region_polygons(PlanarSlice slice, List<GeneralPolygon2d> polygons, PrintMeshOptions options)
        {
            slice.AddCropRegions(polygons);
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



        static bool compute_plane_curves(DMesh3 mesh, DMeshAABBTree3 spatial, 
            double z, bool is_solid,
            out Polygon2d[] loops, out PolyLine2d[] curves )
        {
            Func<Vector3d, double> planeF = (v) => {
                return v.z - z;
            };

            // find list of triangles that intersect this z-value
            PlaneIntersectionTraversal planeIntr = new PlaneIntersectionTraversal(mesh, z);
            spatial.DoTraversal(planeIntr);
            List<int> triangles = planeIntr.triangles;

            // compute intersection iso-curves, which produces a 3D graph of undirected edges
            MeshIsoCurves iso = new MeshIsoCurves(mesh, planeF) { WantGraphEdgeInfo = true };
            iso.Compute(triangles);
            DGraph3 graph = iso.Graph;
            if ( graph.EdgeCount == 0 ) {
                loops = new Polygon2d[0];
                curves = new PolyLine2d[0];
                return false;
            }

            // if this is a closed solid, any open spurs in the graph are errors
            if (is_solid)
                DGraph3Util.ErodeOpenSpurs(graph);

            // [RMS] debug visualization
            //DGraph2 graph2 = new DGraph2();
            //Dictionary<int, int> mapV = new Dictionary<int, int>();
            //foreach (int vid in graph.VertexIndices())
            //    mapV[vid] = graph2.AppendVertex(graph.GetVertex(vid).xy);
            //foreach (int eid in graph.EdgeIndices())
            //    graph2.AppendEdge(mapV[graph.GetEdge(eid).a], mapV[graph.GetEdge(eid).b]);
            //SVGWriter svg = new SVGWriter();
            //svg.AddGraph(graph2, SVGWriter.Style.Outline("black", 0.05f));
            //foreach (int vid in graph2.VertexIndices()) {
            //    if (graph2.IsJunctionVertex(vid))
            //        svg.AddCircle(new Circle2d(graph2.GetVertex(vid), 0.25f), SVGWriter.Style.Outline("red", 0.1f));
            //    else if (graph2.IsBoundaryVertex(vid))
            //        svg.AddCircle(new Circle2d(graph2.GetVertex(vid), 0.25f), SVGWriter.Style.Outline("blue", 0.1f));
            //}
            //svg.Write(string.Format("c:\\meshes\\EXPORT_SLICE_{0}.svg", z));

            // extract loops and open curves from graph
            DGraph3Util.Curves c = DGraph3Util.ExtractCurves(graph, false, iso.ShouldReverseGraphEdge);
            loops = new Polygon2d[c.Loops.Count];
            for (int li = 0; li < loops.Length; ++li) {
                DCurve3 loop = c.Loops[li];
                loops[li] = new Polygon2d();
                foreach (Vector3d v in loop.Vertices) 
                    loops[li].AppendVertex(v.xy);
            }

            curves = new PolyLine2d[c.Paths.Count];
            for (int pi = 0; pi < curves.Length; ++pi) {
                DCurve3 span = c.Paths[pi];
                curves[pi] = new PolyLine2d();
                foreach (Vector3d v in span.Vertices) 
                    curves[pi].AppendVertex(v.xy);
            }

            return true;
        }



        class PlaneIntersectionTraversal : DMeshAABBTree3.TreeTraversal
        {
            public DMesh3 Mesh;
            public double Z;
            public List<int> triangles = new List<int>();
            public PlaneIntersectionTraversal(DMesh3 mesh, double z)
            {
                this.Mesh = mesh;
                this.Z = z;
                this.NextBoxF = (box, depth) => {
                    return (Z >= box.Min.z && Z <= box.Max.z);
                };
                this.NextTriangleF = (tID) => {
                    AxisAlignedBox3d box = Mesh.GetTriBounds(tID);
                    if (Z >= box.Min.z && z <= box.Max.z)
                        triangles.Add(tID);
                };
            }
        }


	}
}

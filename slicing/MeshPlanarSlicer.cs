using System;
using System.Collections.Generic;
using System.Threading;
using g3;

namespace gs
{
	public class MeshPlanarSlicer
	{
        class SliceMesh
        {
            public DMesh3 mesh;
            public AxisAlignedBox3d bounds;

            public PrintMeshOptions options;
        }
        List<SliceMesh> Meshes = new List<SliceMesh>();

		public double LayerHeightMM = 0.2;
        public double OpenPathDefaultWidthMM = 0.4;
		public int MaxLayerCount = 10000;		// just for sanity-check

		public enum SliceLocations {
			Base, EpsilonBase, MidLine
		}
		public SliceLocations SliceLocation = SliceLocations.MidLine;

        public PrintMeshOptions.OpenPathsModes DefaultOpenPathMode = PrintMeshOptions.OpenPathsModes.Clipped;

        // these can be used for progress tracking
        public int TotalCompute = 0;
        public int Progress = 0;

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
            return AddMesh(mesh, PrintMeshOptions.Default);
        }


        public bool Add(PrintMeshAssembly assy)
        {
            foreach ( var pair in assy.MeshesAndOptions()) 
                AddMesh(pair.Item1, pair.Item2);
            return true;
        }




		public PlanarSliceStack Compute()
		{
			Interval1d zrange = Interval1d.Empty;
			foreach ( var meshinfo in Meshes ) {
				zrange.Contain(meshinfo.bounds.Min.z);
				zrange.Contain(meshinfo.bounds.Max.z);
			}

			int nLayers = (int)(zrange.Length / LayerHeightMM);
			if (nLayers > MaxLayerCount)
				throw new Exception("MeshPlanarSlicer.Compute: exceeded layer limit. Increase .MaxLayerCount.");

            // make list of slice heights (could be irregular)
            List<double> heights = new List<double>();
			for (int i = 0; i < nLayers + 1; ++i) {
				double t = zrange.a + (double)i * LayerHeightMM;
				if (SliceLocation == SliceLocations.EpsilonBase)
					t += 0.01 * LayerHeightMM;
				else if (SliceLocation == SliceLocations.MidLine)
					t += 0.5 * LayerHeightMM;
				heights.Add(t);
			}
			int NH = heights.Count;

			// process each *slice* in parallel
			PlanarSlice[] slices = new PlanarSlice[NH];
            for (int i = 0; i < NH; ++i) {
                slices[i] = new PlanarSlice() { Z = heights[i] };
                slices[i].EmbeddedPathWidth = OpenPathDefaultWidthMM;
            }

            TotalCompute = Meshes.Count * nLayers;
            Progress = 0;


            // compute slices separately for each mesh
            for (int mi = 0; mi < Meshes.Count; ++mi ) {
				DMesh3 mesh = Meshes[mi].mesh;
                PrintMeshOptions mesh_options = Meshes[mi].options;

                // [TODO] should we hang on to this spatial? or should it be part of assembly?
                DMeshAABBTree3 spatial = new DMeshAABBTree3(mesh, true);
				AxisAlignedBox3d bounds = Meshes[mi].bounds;

                bool is_support = mesh_options.IsSupport;
                bool is_closed = (mesh_options.IsOpen) ? false : mesh.IsClosed();
                var useOpenMode = (mesh_options.OpenPathMode == PrintMeshOptions.OpenPathsModes.Default) ?
                    DefaultOpenPathMode : mesh_options.OpenPathMode;

                // each layer is independent so we can do in parallel
                gParallel.ForEach(Interval1i.Range(NH), (i) => {
					double z = heights[i];
					if (z < bounds.Min.z || z > bounds.Max.z)
						return;

                    // compute cut
                    Polygon2d[] polys; PolyLine2d[] paths;
                    compute_plane_curves(mesh, spatial, z, out polys, out paths);

                    // if we didn't hit anything, try again with jittered plane
                    // [TODO] this could be better...
                    if ( (is_closed && polys.Length == 0) || (is_closed == false &&  polys.Length == 0 && paths.Length == 0)) {
                        compute_plane_curves(mesh, spatial, z+LayerHeightMM*0.25, out polys, out paths);
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

						PlanarComplex.SolidRegionInfo solids =
							complex.FindSolidRegions(options);

                        if (is_support)
                            slices[i].AddSupportPolygons(solids.Polygons);
                        else
    						slices[i].AddPolygons(solids.Polygons);

                    } else if (useOpenMode != PrintMeshOptions.OpenPathsModes.Ignored) {

                        foreach (PolyLine2d pline in paths) {
                            if (useOpenMode == PrintMeshOptions.OpenPathsModes.Embedded )
                                slices[i].AddEmbeddedPath(pline);   
                            else
                                slices[i].AddClippedPath(pline);
                        }

                        // [TODO] 
                        //   - does not really handle clipped polygons properly, there will be an extra break somewhere...
                        foreach (Polygon2d poly in polys) {
                            PolyLine2d pline = new PolyLine2d(poly, true);
                            if (useOpenMode == PrintMeshOptions.OpenPathsModes.Embedded)
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
                slices[i].Resolve();
            });

            // discard spurious empty slices
            int last = slices.Length-1;
            while (slices[last].IsEmpty && last > 0)
                last--;
            int first = 0;
            while (slices[first].IsEmpty && first < slices.Length)
                first++;

            PlanarSliceStack stack = new PlanarSliceStack();
            for (int k = first; k <= last; ++k)
                stack.Add(slices[k]);

			return stack;
		}



        static bool compute_plane_curves(DMesh3 mesh, DMeshAABBTree3 spatial, double z, out Polygon2d[] loops, out PolyLine2d[] curves )
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

            // extract loops and open curves from graph
            DGraph3Util.Curves c = DGraph3Util.ExtractCurves(graph, iso.ShouldReverseGraphEdge);
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

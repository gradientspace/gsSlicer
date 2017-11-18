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

            // this sucks. oh well!
            for (int mi = 0; mi < Meshes.Count; ++mi ) {
				DMesh3 mesh = Meshes[mi].mesh;
				AxisAlignedBox3d bounds = Meshes[mi].bounds;

                bool closed = (Meshes[mi].options.IsOpen) ? false : mesh.IsClosed();

                var useOpenMode = (Meshes[mi].options.OpenPathMode == PrintMeshOptions.OpenPathsModes.Default) ?
                    DefaultOpenPathMode : Meshes[mi].options.OpenPathMode;

                // each layer is independent because we are slicing new mesh
                gParallel.ForEach(Interval1i.Range(NH), (i) => {
					double z = heights[i];
					if (z < bounds.Min.z || z > bounds.Max.z)
						return;

					// compute cut
					DMesh3 sliceMesh = new DMesh3(mesh);
					MeshPlaneCut cut = new MeshPlaneCut(sliceMesh, new Vector3d(0, 0, z), Vector3d.AxisZ);
					cut.Cut();


                    // in pathological cases (eg two stacked cubes) the cutting plane can cut right
                    // between the two cubes, hitting neither. So, if we get no loops, try jittering the plane a bit
                    if ( (closed && cut.CutLoops.Count == 0) || (closed == false && cut.CutSpans.Count == 0) ) {
                        sliceMesh = new DMesh3(mesh);
                        cut = new MeshPlaneCut(sliceMesh, new Vector3d(0, 0, z + LayerHeightMM*0.25), Vector3d.AxisZ);
                        cut.Cut();
                    }

                    Polygon2d[] polys = new Polygon2d[cut.CutLoops.Count];
                    for ( int li = 0; li < polys.Length; ++li) {
                        EdgeLoop loop = cut.CutLoops[li];
                        polys[li] = new Polygon2d();
                        foreach (int vid in loop.Vertices) {
                            Vector3d v = sliceMesh.GetVertex(vid);
                            polys[li].AppendVertex(v.xy);
                        }
                    }

                    PolyLine2d[] paths = new PolyLine2d[cut.CutSpans.Count];
                    for (int pi = 0; pi < paths.Length; ++pi) {
                        EdgeSpan span = cut.CutSpans[pi];
                        paths[pi] = new PolyLine2d();
                        foreach (int vid in span.Vertices) {
                            Vector3d v = sliceMesh.GetVertex(vid);
                            paths[pi].AppendVertex(v.xy);
                        }
                    }

                    if (closed) {

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

	}
}

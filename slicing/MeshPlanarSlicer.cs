using System;
using System.Collections.Generic;
using System.Threading;
using g3;

namespace gs
{
	public class MeshPlanarSlicer
	{
		List<DMesh3> Meshes = new List<DMesh3>();
		List<AxisAlignedBox3d> Bounds = new List<AxisAlignedBox3d>();

		public double LayerHeightMM = 0.2;
        public double OpenPathDefaultWidthMM = 0.4;
		public int MaxLayerCount = 10000;		// just for sanity-check

		public enum SliceLocations {
			Base, EpsilonBase, MidLine
		}
		public SliceLocations SliceLocation = SliceLocations.MidLine;


        public enum OpenPathsModes
        {
            Embedded = 0, Clipped = 1, Ignored = 2
        }
        public OpenPathsModes DefaultOpenPathMode = OpenPathsModes.Embedded;


        // these can be used for progress tracking
        public int TotalCompute = 0;
        public int Progress = 0;


		public MeshPlanarSlicer()
		{
		}

		public bool AddMesh(DMesh3 mesh) {
			Meshes.Add(mesh);
			Bounds.Add(mesh.CachedBounds);

            return true;
		}
		public bool AddMeshes(IEnumerable<DMesh3> meshes) {
            bool ok = true;
            foreach (var m in meshes) {
                if (!AddMesh(m))
                    ok = false;
            }
            return ok;
		}


		public PlanarSliceStack Compute()
		{
			Interval1d zrange = Interval1d.Empty;
			foreach ( var b in Bounds ) {
				zrange.Contain(b.Min.z);
				zrange.Contain(b.Max.z);
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
				DMesh3 mesh = Meshes[mi];
				AxisAlignedBox3d bounds = Bounds[mi];
                bool closed = mesh.IsClosed();

				// each layer is independent because we are slicing new mesh
				gParallel.ForEach(Interval1i.Range(NH), (i) => {
					double z = heights[i];
					if (z < bounds.Min.z || z > bounds.Max.z)
						return;

					// compute cut
					DMesh3 sliceMesh = new DMesh3(mesh);
					MeshPlaneCut cut = new MeshPlaneCut(sliceMesh, new Vector3d(0, 0, z), Vector3d.AxisZ);
					cut.Cut();

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
                        // extract slice polygons
                        PlanarComplex complex = new PlanarComplex();
                        foreach (Polygon2d poly in polys)
                            complex.Add(poly);

                        PlanarComplex.SolidRegionInfo solids =
                            complex.FindSolidRegions(0.001, false);

                        slices[i].AddPolygons(solids.Polygons);

                    } else if (DefaultOpenPathMode != OpenPathsModes.Ignored) {

                        foreach (PolyLine2d pline in paths) {
                            if ( DefaultOpenPathMode == OpenPathsModes.Embedded )
                                slices[i].AddEmbeddedPath(pline);   
                            else
                                slices[i].AddClippedPath(pline);
                        }

                        // [TODO] 
                        //   - does not really handle clipped polygons properly, there will be an extra break somewhere...
                        foreach (Polygon2d poly in polys) {
                            PolyLine2d pline = new PolyLine2d(poly, true);
                            if (DefaultOpenPathMode == OpenPathsModes.Embedded)
                                slices[i].AddEmbeddedPath(pline);
                            else
                                slices[i].AddClippedPath(pline);
                        }
                    }

                    Interlocked.Increment(ref Progress);
				});  // end of parallel.foreach
				              
			} // end mesh iter

            // resolve planar intersections, etc
            for (int i = 0; i < NH; ++i)
                slices[i].Resolve();

            PlanarSliceStack stack = new PlanarSliceStack();
			stack.Add(slices);

			return stack;
		}

	}
}

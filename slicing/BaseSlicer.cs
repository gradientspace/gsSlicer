using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using g3;

namespace gs
{
    public class BaseSlicer
    {
        /// <summary>
        /// internally we Math.Round() to this precision
        /// </summary>
        public int PrecisionDigits = 4;


        protected class SliceMesh
        {
            public DMesh3 mesh;
            public AxisAlignedBox3d bounds;

            public PrintMeshOptions options;
        }
        protected List<SliceMesh> Meshes = new List<SliceMesh>();







        /*
         *  Process and Cancel support
         */

        public int TotalCompute = 0;
        public int Progress = 0;

        public Func<bool> CancelF = () => { return false; };
        public bool WasCancelled = false;


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








        /*
         * Support for slicing mesh
         */

        protected bool ComputeSlicePlaneCurves(DMesh3 mesh, DMeshAABBTree3 spatial,
            double z, bool is_solid,
            out Polygon2d[] loops, out PolyLine2d[] curves)
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
            if (graph.EdgeCount == 0) {
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



        protected class PlaneIntersectionTraversal : DMeshAABBTree3.TreeTraversal
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














        /*
         *  Support for finding and handling horizontal regions
         */

        protected class PlanarRegion
        {
            public DMesh3 Mesh;
            public double Z;
            public int[] Triangles;
        }


        /// <summary>
        /// Find regions of the input meshes that are horizontal, returns binned by Z-value.
        /// Currently only finds upward-facing regions.
        /// </summary>
        protected Dictionary<double, List<PlanarRegion>> FindPlanarZRegions(double minDimension, double dotNormalTol = 0.999)
        {
            Dictionary<double, List<PlanarRegion>> Regions = new Dictionary<double, List<PlanarRegion>>();
            SpinLock region_lock = new SpinLock();

            gParallel.ForEach(Meshes, (sliceMesh) => {
                if (sliceMesh.options.IsCavity == false)
                    return;
                DMesh3 mesh = sliceMesh.mesh;

                HashSet<int> planar_tris = new HashSet<int>();
                foreach (int tid in mesh.TriangleIndices()) {
                    Vector3d n = mesh.GetTriNormal(tid);
                    double dot = n.Dot(Vector3d.AxisZ);
                    if (dot > dotNormalTol)
                        planar_tris.Add(tid);
                }

                MeshConnectedComponents regions = new MeshConnectedComponents(mesh);
                regions.FilterF = planar_tris.Contains;
                regions.FindConnectedT();
                foreach (var c in regions) {
                    AxisAlignedBox3d bounds = MeshMeasurements.BoundsT(mesh, c.Indices);
                    if (bounds.Width > minDimension && bounds.Height > minDimension) {
                        double z = Math.Round(bounds.Center.z, PrecisionDigits);

                        PlanarRegion planar = new PlanarRegion() { Mesh = mesh, Z = z, Triangles = c.Indices };

                        bool taken = false;
                        region_lock.Enter(ref taken);
                        List<PlanarRegion> zregions;
                        if (Regions.TryGetValue(z, out zregions)) {
                            zregions.Add(planar);
                        } else {
                            zregions = new List<PlanarRegion>() { planar };
                            Regions[z] = zregions;
                        }
                        region_lock.Exit();
                    }
                }
            });

            return Regions;
        }


        protected List<Polygon2d> GetPlanarPolys(List<PlanarRegion> regions)
        {
            List<Polygon2d> polys = new List<Polygon2d>();
            foreach (var r in regions)
                GetPlanarPolys(r, polys);
            return polys;
        }

        protected void GetPlanarPolys(PlanarRegion r, List<Polygon2d> polys)
        {
            try {
                MeshRegionBoundaryLoops loops = new MeshRegionBoundaryLoops(r.Mesh, r.Triangles);
                foreach (var loop in loops) {
                    DCurve3 curve = loop.ToCurve();
                    Polygon2d poly = new Polygon2d();
                    int NV = curve.VertexCount;
                    for (int k = 0; k < NV; k++)
                        poly.AppendVertex(curve[k].xy);
                    polys.Add(poly);
                }

            } catch (Exception) {
                // add each triangle as a polygon and let clipper sort it out
                Vector3d v0 = Vector3d.Zero, v1 = Vector3d.Zero, v2 = Vector3d.Zero;
                foreach ( int tid in r.Triangles ) {
                    r.Mesh.GetTriVertices(tid, ref v0, ref v1, ref v2);
                    Polygon2d p = new Polygon2d();
                    p.AppendVertex(v0.xy); p.AppendVertex(v1.xy); p.AppendVertex(v2.xy);
                    polys.Add(p);
                }
            }
        }


    }
}

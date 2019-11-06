﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;

namespace gs
{
    public class PolygonDecomposer
    {
        public static List<Polygon2d> Compute(GeneralPolygon2d poly, double minArea)
        {
            TriangulatedPolygonGenerator generator = new TriangulatedPolygonGenerator()
            {
                Polygon = poly,
                Subdivisions = 16
            };
            DMesh3 mesh = generator.Generate().MakeDMesh();
            return decompose_cluster_up(mesh, minArea);
        }

        private static List<Polygon2d> decompose_cluster_up(DMesh3 mesh, double minArea)
        {
            optimize_mesh(mesh);
            mesh.CompactInPlace();
            mesh.DiscardTriangleGroups();
            mesh.EnableTriangleGroups(0);

            Dictionary<int, double> areas = new Dictionary<int, double>();
            Dictionary<int, HashSet<int>> trisets = new Dictionary<int, HashSet<int>>();
            HashSet<int> active_groups = new HashSet<int>();

            Action<int, int> add_tri_to_group = (tid, gid) => {
                mesh.SetTriangleGroup(tid, gid);
                areas[gid] = areas[gid] + mesh.GetTriArea(tid);
                trisets[gid].Add(tid);
            };
            Action<int, int> add_group_to_group = (gid, togid) => {
                var set = trisets[togid];
                foreach (int tid in trisets[gid])
                {
                    mesh.SetTriangleGroup(tid, togid);
                    set.Add(tid);
                }
                areas[togid] += areas[gid];
                active_groups.Remove(gid);
            };
            Func<IEnumerable<int>, int> find_min_area_group = (tri_itr) => {
                int min_gid = -1; double min_area = double.MaxValue;
                foreach (int tid in tri_itr)
                {
                    int gid = mesh.GetTriangleGroup(tid);
                    double a = areas[gid];
                    if (a < min_area)
                    {
                        min_area = a;
                        min_gid = gid;
                    }
                }
                return min_gid;
            };


            foreach (int eid in MeshIterators.InteriorEdges(mesh))
            {
                Index2i et = mesh.GetEdgeT(eid);
                if (mesh.GetTriangleGroup(et.a) != 0 || mesh.GetTriangleGroup(et.b) != 0)
                    continue;
                int gid = mesh.AllocateTriangleGroup();
                areas[gid] = 0;
                trisets[gid] = new HashSet<int>();
                active_groups.Add(gid);
                add_tri_to_group(et.a, gid);
                add_tri_to_group(et.b, gid);
            }
            foreach (int tid in mesh.TriangleIndices())
            {
                if (mesh.GetTriangleGroup(tid) != 0)
                    continue;
                int gid = find_min_area_group(mesh.TriTrianglesItr(tid));
                add_tri_to_group(tid, gid);
            }


            IndexPriorityQueue pq = new IndexPriorityQueue(mesh.MaxGroupID);
            foreach (var pair in areas)
            {
                pq.Insert(pair.Key, (float)pair.Value);
            }
            while (pq.Count > 0)
            {
                int gid = pq.First;
                pq.Remove(gid);
                if (areas[gid] > minArea)    // ??
                    break;

                List<int> nbr_groups = find_neighbour_groups(mesh, gid, trisets[gid]);
                int min_gid = -1; double min_area = double.MaxValue;
                foreach (int ngid in nbr_groups)
                {
                    double a = areas[ngid];
                    if (a < min_area)
                    {
                        min_area = a;
                        min_gid = ngid;
                    }
                }
                if (min_gid != -1)
                {
                    add_group_to_group(gid, min_gid);
                    pq.Remove(min_gid);
                    pq.Insert(min_gid, (float)areas[min_gid]);
                }
            }



            List<Polygon2d> result = new List<Polygon2d>();
            int[][] sets = FaceGroupUtil.FindTriangleSetsByGroup(mesh);
            foreach (var set in sets)
                result.Add(make_poly(mesh, set));
            return result;
        }

        private static List<int> find_neighbour_groups(DMesh3 mesh, int gid, HashSet<int> group_tris)
        {
            List<int> result = new List<int>();
            foreach (int tid in group_tris)
            {
                foreach (int ntid in mesh.TriTrianglesItr(tid))
                {
                    int ngid = mesh.GetTriangleGroup(ntid);
                    if (ngid != gid && result.Contains(ngid) == false)
                        result.Add(ngid);
                }
            }
            return result;
        }

        private static Polygon2d make_poly(DMesh3 mesh, IEnumerable<int> triangles)
        {
            DSubmesh3 submesh = new DSubmesh3(mesh, triangles);
            MeshBoundaryLoops loops = new MeshBoundaryLoops(submesh.SubMesh);
            Util.gDevAssert(loops.Loops.Count == 1);
            return make_poly(submesh.SubMesh, loops.Loops[0]);
        }

        private static Polygon2d make_poly(DMesh3 mesh, EdgeLoop loop)
        {
            Polygon2d poly = new Polygon2d();
            for (int k = 0; k < loop.VertexCount; ++k)
            {
                Vector3d v = mesh.GetVertex(loop.Vertices[k]);
                poly.AppendVertex(v.xy);
            }
            return poly;
        }

        private static void optimize_mesh(DMesh3 mesh)
        {
            Reducer reducer = new Reducer(mesh);
            MeshConstraints constraints = new MeshConstraints();
            MeshConstraintUtil.FixAllBoundaryEdges(constraints, mesh);
            reducer.SetExternalConstraints(constraints);
            reducer.ReduceToTriangleCount(1);

            Vector3d a, b, c, d;
            a = b = c = d = Vector3d.Zero;

            bool done = false;
            while (!done)
            {
                done = true;

                for (int eid = 0; eid < mesh.MaxEdgeID; ++eid)
                {
                    if (mesh.IsEdge(eid) == false)
                        continue;

                    Index4i evt = mesh.GetEdge(eid);
                    if (evt.d == DMesh3.InvalidID)
                        continue;
                    a = mesh.GetVertex(evt.a); b = mesh.GetVertex(evt.b);
                    Index2i ov = mesh.GetEdgeOpposingV(eid);
                    c = mesh.GetVertex(ov.a); d = mesh.GetVertex(ov.b);

                    if (c.DistanceSquared(d) > a.DistanceSquared(b))
                        continue;
                    if (MeshUtil.CheckIfEdgeFlipCreatesFlip(mesh, eid))
                        continue;

                    DMesh3.EdgeFlipInfo flipInfo;
                    if (mesh.FlipEdge(eid, out flipInfo) == MeshResult.Ok)
                        done = false;
                }
            }


        }

        private static int find_shortest_internal_edge(DMesh3 mesh)
        {
            double shortSqr = double.MaxValue;
            int short_eid = -1;
            Vector3d va, vb; va = vb = Vector3d.Zero;
            foreach (int eid in mesh.VertexIndices())
            {
                if (mesh.IsBoundaryEdge(eid))
                    continue;
                mesh.GetEdgeV(eid, ref va, ref vb);
                double lenSqr = va.DistanceSquared(ref vb);
                if (lenSqr < shortSqr)
                {
                    shortSqr = lenSqr;
                    short_eid = eid;
                }
            }
            return short_eid;
        }
    }
}

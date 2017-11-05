using System;
using System.Collections.Generic;
using g3;

namespace gs
{
    // [TODO] flesh out this class...
    public class PrintMeshAssembly
    {
        class MeshInfo
        {
            public DMesh3 Mesh;

            public DMesh3 SourceMesh;
        }
        List<MeshInfo> meshes = new List<MeshInfo>();



        public IReadOnlyList<DMesh3> Meshes {
            get {
                List<DMesh3> m = new List<DMesh3>();
                foreach (var mi in meshes)
                    m.Add(mi.Mesh);
                return m;
            }
        }



        public void AddMesh(DMesh3 mesh) {
            MeshInfo mi = new MeshInfo() {
                Mesh = mesh,
                SourceMesh = mesh
            };
            meshes.Add(mi);
        }
        public void AddMeshes(IEnumerable<DMesh3> meshes) {
            foreach (var v in meshes)
                AddMesh(v);
        }




        public AxisAlignedBox3d TotalBounds {
            get {
                AxisAlignedBox3d bounds = AxisAlignedBox3d.Empty;
                foreach (var mesh in Meshes)
                    bounds.Contain(mesh.CachedBounds);
                return bounds;
            }
        }

    }


}

using System;
using System.Collections.Generic;
using g3;

namespace gs
{
    /// <summary>
    /// Options for PrintMeshAssembly meshes
    /// </summary>
    public struct PrintMeshOptions
    {
        public bool IsSupport;      // treat as support volume
        public bool IsCavity;       // treat as cavity
        public bool IsOpen;         // treat as open mesh (ie do not fill)

        public enum OpenPathsModes
        {
            Embedded = 0, Clipped = 1, Ignored = 2, Default = 10
        }
        public OpenPathsModes OpenPathMode;


        public static readonly PrintMeshOptions Default = new PrintMeshOptions() {
            IsSupport = false,
            IsCavity = false,
            IsOpen = false,
            OpenPathMode = OpenPathsModes.Default
        };

        public static readonly PrintMeshOptions Support = new PrintMeshOptions() {
            IsSupport = true,
            IsCavity = false,
            IsOpen = false,
            OpenPathMode = OpenPathsModes.Default
        };

        public static readonly PrintMeshOptions Cavity = new PrintMeshOptions() {
            IsSupport = false,
            IsCavity = true,
            IsOpen = false,
            OpenPathMode = OpenPathsModes.Default
        };
    }




    /// <summary>
    /// Represents set of print meshes and per-mesh options
    /// [TODO] this could be more useful...also we might want to include more than just meshes?
    /// </summary>
    public class PrintMeshAssembly
    {
        class MeshInfo
        {
            public DMesh3 Mesh;
            public PrintMeshOptions Options;
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


        public IEnumerable<Tuple<DMesh3,PrintMeshOptions>> MeshesAndOptions()
        {
            foreach (var mi in meshes)
                yield return new Tuple<DMesh3, PrintMeshOptions>(mi.Mesh, mi.Options);
        }


        public void AddMesh(DMesh3 mesh, PrintMeshOptions options)
        {
            MeshInfo mi = new MeshInfo() {
                Mesh = mesh,
                Options = options
            };
            meshes.Add(mi);
        }
        public void AddMesh(DMesh3 mesh) {
            AddMesh(mesh, PrintMeshOptions.Default);
        }

        public void AddMeshes(IEnumerable<DMesh3> meshes) {
            AddMeshes(meshes, PrintMeshOptions.Default);
        }
        public void AddMeshes(IEnumerable<DMesh3> meshes, PrintMeshOptions options) {
            foreach (var v in meshes)
                AddMesh(v, options);
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

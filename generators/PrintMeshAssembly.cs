using System;
using System.Collections.Generic;
using g3;

namespace gs
{
    // [TODO] flesh out this class...
    public class PrintMeshAssembly
    {
        public List<DMesh3> Meshes = new List<DMesh3>();


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

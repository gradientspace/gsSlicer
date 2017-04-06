using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;

namespace gs
{
    public class PathSet2
    {
        public List<PolyLine2d> Paths = new List<PolyLine2d>();



        public AxisAlignedBox2d Bounds
        {
            get {
                AxisAlignedBox2d box = AxisAlignedBox2d.Empty;
                foreach (PolyLine2d p in Paths)
                    box.Contain(p.GetBounds());
                return box;
            }
        }
    }
}

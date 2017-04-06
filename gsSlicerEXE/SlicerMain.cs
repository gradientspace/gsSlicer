using System;
using System.Collections.Generic;
using System.Linq;
using g3;

namespace gsSlicer
{
    static class SlicerMain
    {

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Polygon2d poly = new Polygon2d();
            poly.AppendVertex(new Vector2d(0, 0));
            poly.AppendVertex(new Vector2d(100, 0));
            poly.AppendVertex(new Vector2d(100, 100));
            poly.AppendVertex(new Vector2d(0, 1000));


            

        }

    }
}

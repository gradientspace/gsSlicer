using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	public class FillPaths2d
	{
		public List<Polygon2d> Loops = new List<Polygon2d>();
		public List<PolyLine2d> Curves = new List<PolyLine2d>();

		public FillPaths2d()
		{
		}


		public void Append(GeneralPolygon2d poly) {
			Loops.Add(new Polygon2d(poly.Outer));
			foreach (var h in poly.Holes)
				Loops.Add(new Polygon2d(h));
		}

		public void Append(List<GeneralPolygon2d> polys) {
			foreach (var p in polys)
				Append(p);
		}
	}
}

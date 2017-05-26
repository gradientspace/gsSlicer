﻿using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	public class PlanarSlice
	{
		public double Z = 0;
		public List<GeneralPolygon2d> Solids = new List<GeneralPolygon2d>();

		// [TODO] sheets ?

		public PlanarSlice()
		{
		}

		public void Add(GeneralPolygon2d poly) {
			Solids.Add(poly);
		}
		public void Add(IEnumerable<GeneralPolygon2d> polys) {
			foreach (GeneralPolygon2d p in polys)
				Add(p);
		}

		public AxisAlignedBox2d Bounds {
			get {
				AxisAlignedBox2d box = AxisAlignedBox2d.Empty;
				foreach (GeneralPolygon2d poly in Solids)
					box.Contain(poly.Outer.GetBounds());
				return box;
			}
		}

	}
}

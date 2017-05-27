﻿using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	public class PlanarSliceStack
	{
		public List<PlanarSlice> Slices = new List<PlanarSlice>();

		public PlanarSliceStack()
		{
		}

		public void Add(PlanarSlice slice) {
			Slices.Add(slice);
		}
		public void Add(IEnumerable<PlanarSlice> slices) {
			foreach (var s in slices)
				Add(s);
		}



		public AxisAlignedBox3d Bounds {
			get {
				AxisAlignedBox3d box = AxisAlignedBox3d.Empty;
				foreach (PlanarSlice slice in Slices) {
					AxisAlignedBox2d b = slice.Bounds;
					box.Contain(new Vector3d(b.Min.x, b.Min.y, slice.Z));
					box.Contain(new Vector3d(b.Max.x, b.Max.y, slice.Z));
				}
				return box;
			}
		}

	}
}

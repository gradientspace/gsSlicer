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


	}
}

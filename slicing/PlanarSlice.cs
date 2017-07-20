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


        // allow integer tags on polygons, which we can use for arbitrary stuff
        public IntTagSet<GeneralPolygon2d> Tags {
            get {
                if (tags == null)
                    tags = new IntTagSet<GeneralPolygon2d>();
                return tags;
            }
        }
        IntTagSet<GeneralPolygon2d> tags;


		public PlanarSlice()
		{
		}

		public void Add(GeneralPolygon2d poly) {
            if (poly.Outer.IsClockwise)
                poly.Outer.Reverse();

            Solids = ClipperUtil.PolygonBoolean(Solids, poly, ClipperUtil.BooleanOp.Union);
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

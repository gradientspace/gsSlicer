using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	public class ShellsFillPolygon : IFillPolygon
	{
		// polygon to fill
		public GeneralPolygon2d Polygon { get; set; }

		// parameters
		public double ToolWidth = 0.4;
		public double PathSpacing = 0.4;
		public int Layers = 2;

		// if true, we inset half of tool-width from Polygon,
		// otherwise first layer is polygon
		public bool InsetFromInputPolygon = true;

		// shell layers
		public List<FillPaths2d> Shells { get; set; }

		// remaining interior polygons (to fill w/ other strategy?)
		public List<GeneralPolygon2d> InnerPolygons { get; set; }


		public ShellsFillPolygon(GeneralPolygon2d poly)
		{
			Polygon = poly;
			Shells = new List<FillPaths2d>();
		}


		public bool Compute()
		{
			// first shell is either polygon, or inset from that polygon
			List<GeneralPolygon2d> current = (InsetFromInputPolygon) ?
				ClipperUtil.ComputeOffsetPolygon(Polygon, -ToolWidth / 2, true) :
			   	new List<GeneralPolygon2d>() { Polygon };
 			
			// convert previous layer to shell, and then compute next layer
			for (int i = 0; i < Layers; ++i ) {
				FillPaths2d paths = new FillPaths2d();
				paths.Append(current);
				Shells.Add(paths);

				List<GeneralPolygon2d> all_next = new List<GeneralPolygon2d>();
				foreach ( GeneralPolygon2d gpoly in current ) {
					List<GeneralPolygon2d> offsets =
						ClipperUtil.ComputeOffsetPolygon(gpoly, -ToolWidth, true);
					all_next.AddRange(offsets);
				}

				current = all_next;
			}

			// remaining inner polygons
			InnerPolygons = current;
			return true;
		}
	}
}

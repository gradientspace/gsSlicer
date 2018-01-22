﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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


        public PlanarSlice this[int i] {
            get { return Slices[i]; }
        }

        public int Count
        {
            get { return Slices.Count; }
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







        /// <summary>
        /// Format is:
        /// [num_slices]
        /// [slice0_z]
        /// [num_polys_in_slice_0]
        /// [x0 y0 x1 y1 x2 y3 ...]
        /// [x0 y0 x1 y1 ... ]
        /// [slice1_z]
        /// [num_polys_in_slice_1]
        /// ...
        /// </summary>
        public void ReadSimpleSliceFormat(TextReader reader)
        {
            PlanarComplex.FindSolidsOptions options = PlanarComplex.FindSolidsOptions.SortPolygons;
            options.TrustOrientations = false;
            char[] splitchars = new char[] { ' ' };

            int nSlices = int.Parse(reader.ReadLine());
            PlanarComplex[] layer_complexes = new PlanarComplex[nSlices];

            for ( int si = 0; si < nSlices; ++si ) {
                PlanarSlice slice = new PlanarSlice();
                slice.Z = double.Parse(reader.ReadLine());

                PlanarComplex complex = new PlanarComplex();

                int nPolys = int.Parse(reader.ReadLine());
                for ( int pi = 0; pi < nPolys; pi++) {
                    string[] stringValues = reader.ReadLine().Split(splitchars, StringSplitOptions.RemoveEmptyEntries);
                    double[] values = Array.ConvertAll( stringValues, Double.Parse);
                    Polygon2d poly = new Polygon2d(values);
                    complex.Add(poly);
                }

                layer_complexes[si] = complex;

                //// this could be done in separate thread...
                //var solidInfo = complex.FindSolidRegions(options);
                //slice.InputSolids = solidInfo.Polygons;
                //slice.Resolve();

                Slices.Add(slice);
            }


            gParallel.ForEach(Interval1i.Range(nSlices), (si) => {
                var solidInfo = layer_complexes[si].FindSolidRegions(options);
                Slices[si].InputSolids = solidInfo.Polygons;
                Slices[si].Resolve();
            });

        }






        public DMesh3 Make3DTubes(Interval1i layer_range, double merge_tol, double tube_radius)
        {
            Polygon2d tube_profile = Polygon2d.MakeCircle(tube_radius, 8);
            Frame3f frame = Frame3f.Identity;
            
            DMesh3 full_mesh = new DMesh3();
            foreach (int layer_i in layer_range) { 
                PlanarSlice slice = Slices[layer_i];
                frame.Origin = new Vector3f(0,0,slice.Z);
                foreach ( GeneralPolygon2d gpoly in slice.Solids ) {
                    List<Polygon2d> polys = new List<Polygon2d>() { gpoly.Outer }; polys.AddRange(gpoly.Holes);
                    foreach (Polygon2d poly in polys) {
                        Polygon2d simpPoly = new Polygon2d(poly);
                        simpPoly.Simplify(merge_tol, 0.01, true);
                        if (simpPoly.VertexCount < 3)
                            Util.gBreakToDebugger();
                        TubeGenerator tubegen = new TubeGenerator(simpPoly, frame, tube_profile) { NoSharedVertices = true };
                        DMesh3 tubeMesh = tubegen.Generate().MakeDMesh();
                        MeshEditor.Append(full_mesh, tubeMesh);
                    }
                }
            }

            return full_mesh;            
        }



	}
}

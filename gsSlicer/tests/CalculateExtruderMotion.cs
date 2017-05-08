using System;
using System.Collections.Generic;
using g3;

namespace gs 
{
	public class CalculateExtruderMotion 
	{
		public PathSet Paths;

		public double FilamentDiam = 1.75;
		public double NozzleDiam = 0.4;
		public double LayerHeight = 0.2;

		public double HackCorrection = 1.052;

		public double FixedRetractDistance = 1.3;



		public CalculateExtruderMotion(PathSet paths) 
		{
			Paths = paths;
		}


		public double calculate_extrude(double dist, double moveRate) {


			double section_area = NozzleDiam * LayerHeight * HackCorrection;
			double linear_vol = dist * section_area;

			double fil_rad = FilamentDiam/2;
			double fil_area = Math.PI*fil_rad*fil_rad;
			double fil_len = linear_vol / fil_area;

			return fil_len;
		}




		public void Calculate()
		{
			double curA = 0;
			Vector3d curPos = Vector3d.Zero;
			double curRate = 0;

			bool inRetract = false;

			// filter paths
			List<IPath> allPaths = new List<IPath>();
			foreach ( IPath ipath in Paths ) {
				PathUtil.ApplyToLeafPaths(ipath, (p) => { 
					if (p is LinearPath3<PathVertex> || p is ResetExtruderPathHack) { 
						allPaths.Add(p); 
					} 
				});
			}
			int N = allPaths.Count;
			System.Console.WriteLine("CalculateExtruderMotion: have {0} paths", N);


			for ( int pi = 0; pi < N; ++pi ) {
				if ( allPaths[pi] is ResetExtruderPathHack ) {
					curA = 0;
					continue;
				}

				LinearPath3<PathVertex> path = allPaths[pi] as LinearPath3<PathVertex>;
				if ( path == null )
					throw new Exception("Invalid path type!");
				if ( ! (path.Type == PathTypes.Deposition || path.Type == PathTypes.PlaneChange || path.Type == PathTypes.Travel) )
					throw new Exception("Unknown path type!");

				for ( int i = 0; i < path.VertexCount; ++i ) {
					bool last_vtx = (i == path.VertexCount-1);

					Vector3d newPos = path[i].Position;
					double newRate = path[i].FeedRate;

					if ( path.Type != PathTypes.Deposition ) {
						if ( ! inRetract ) {
							curA -= FixedRetractDistance;
							inRetract = true;
						} else {
							if ( last_vtx ) {
								curA += FixedRetractDistance;
								inRetract = false;
							}
						}
						curPos = newPos;
						curRate = newRate;

					} else {
						double dist = (newPos - curPos).Length;
						curPos = newPos;
						curRate = newRate;

						double feed = calculate_extrude(dist, curRate);
						curA += feed;
					}

					PathVertex v = path[i];
					v.Extrusion = GCodeUtil.Extrude(curA);
					path.UpdateVertex(i, v);

				}


			}

		}







		public void TestCalculation()
		{
			double curA = 0;
			Vector3d curPos = Vector3d.Zero;
			double curRate = 0;

			bool inRetract = false;

			// filter paths
			List<IPath> allPaths = new List<IPath>();
			foreach ( IPath ipath in Paths ) {
				PathUtil.ApplyToLeafPaths(ipath, (p) => { 
					if (p is LinearPath3<PathVertex> || p is ResetExtruderPathHack) { 
						allPaths.Add(p); 
					} 
				});
			}
			int N = allPaths.Count;
			System.Console.WriteLine("CalculateExtruderMotion: have {0} paths", N);


			for ( int pi = 0; pi < N; ++pi ) {
				if ( allPaths[pi] is ResetExtruderPathHack ) {
					curA = 0;
					continue;
				}

				LinearPath3<PathVertex> path = allPaths[pi] as LinearPath3<PathVertex>;
				if ( path == null )
					throw new Exception("Invalid path type!");

				for ( int i = 0; i < path.VertexCount; ++i ) {
					bool last_vtx = (i == path.VertexCount-1);
					    
					Vector3d newPos = path[i].Position;
					double newRate = path[i].FeedRate;

					bool send_a = false;
					string comment = "";

					if ( path.Type != PathTypes.Deposition ) {
						if ( ! inRetract ) {
							curA -= FixedRetractDistance;
							send_a = true;
							comment = "retract";
							inRetract = true;
						} else {
							if ( last_vtx ) {
								curA += FixedRetractDistance;
								send_a = true;
								comment = "restart";
								inRetract = false;
							}
						}
						curPos = newPos;
						curRate = newRate;

					} else {
						double dist = (newPos - curPos).Length;
						curPos = newPos;
						curRate = newRate;

						double feed = calculate_extrude(dist, curRate);
						curA += feed;
						send_a = true;
					}

					Vector3d extrudePos = path[i].Extrusion;

					if ( send_a )
						System.Console.WriteLine("G1 X{0:F3} Y{1:F3} Z{2:F3} F{3:F0} A{4:F5}; {5}  realA{6:F5} errA{7:F5}", 
						                         curPos.x, curPos.y, curPos.z, curRate, curA, comment, path[i].Extrusion.x, (curA-path[i].Extrusion.x));
					else
						System.Console.WriteLine("G1 X{0:F3} Y{1:F3} Z{2:F3} F{3:F0}; {4}  {5:F5}", 
						                         curPos.x, curPos.y, curPos.z, curRate, comment, path[i].Extrusion.x);
				}


			}


		}


	}
}

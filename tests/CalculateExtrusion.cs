using System;
using System.Collections.Generic;
using g3;

namespace gs 
{
    /// <summary>
    /// This class implements calculation of the filament extrusion distance/volume along
    /// a ToolpathSet. Currently the actual extrusion calculation is quite basic.
    /// 
    /// Note that this is (currently) also where retraction happens. Possibly this would
    /// be better handled elsewhere, since ideally it is a temporary +/- to the extrusion,
    /// such that the actual accumulated extrusion amount is not modified.
    /// 
    /// </summary>
	public class CalculateExtrusion 
	{
		public ToolpathSet Paths;
		public SingleMaterialFFFSettings Settings;

        public bool EnableRetraction = true;

		double FilamentDiam = 1.75;
		double NozzleDiam = 0.4;
		double LayerHeight = 0.2;
		double FixedRetractDistance = 1.3;
        double SupportExtrudeScale = 0.9;

        // [RMS] if we travel less than this distance, we don't retract
        double MinRetractTravelLength = 2.5;

		// [RMS] this is a fudge factor thatwe maybe should not use?
		//public double HackCorrection = 1.052;    // fitting to error from makerbot slicer
		public double HackCorrection = 1.0;


		// output statistics
		public int NumPaths = 0;
		public double ExtrusionLength = 0;


		public CalculateExtrusion(ToolpathSet paths, SingleMaterialFFFSettings settings) 
		{
			Paths = paths;
			Settings = settings;

            EnableRetraction = settings.EnableRetraction;
            FilamentDiam = settings.Machine.FilamentDiamMM;
			NozzleDiam = settings.Machine.NozzleDiamMM;
			LayerHeight = settings.LayerHeightMM;
			FixedRetractDistance = settings.RetractDistanceMM;
            MinRetractTravelLength = settings.MinRetractTravelLength;
            SupportExtrudeScale = settings.SupportVolumeScale;
        }

		/// <summary>
		/// This function computes the amount of filament to extrude (ie how
		/// much to turn extruder stepper) along pathLen distance, at moveRate speed.
		/// volumeScale allows for local tuning of this.
		/// </summary>
		public double calculate_extrude(double pathLen, double moveRate, double volumeScale = 1.0) 
		{
			double section_area = NozzleDiam * LayerHeight * HackCorrection;
			double linear_vol = pathLen * section_area;
			linear_vol *= volumeScale;

			double fil_rad = FilamentDiam/2;
			double fil_area = Math.PI*fil_rad*fil_rad;
			double fil_len = linear_vol / fil_area;

			return fil_len;
		}




		public void Calculate(Vector3d vStartPos, double fStartA, bool alreadyInRetract = false)
		{
			double curA = fStartA;
			Vector3d curPos = vStartPos;
			double curRate = 0;

			bool inRetract = alreadyInRetract;

			// filter paths
			List<IToolpath> allPaths = new List<IToolpath>();
			foreach ( IToolpath ipath in Paths ) {
				ToolpathUtil.ApplyToLeafPaths(ipath, (p) => { 
					if (p is LinearToolpath3<PrintVertex> || p is ResetExtruderPathHack) { 
						allPaths.Add(p); 
					} 
				});
			}
			int N = allPaths.Count;


            LinearToolpath3<PrintVertex> prev_path = null;
            for ( int pi = 0; pi < N; ++pi ) {
				if ( allPaths[pi] is ResetExtruderPathHack ) {
					curA = 0;
					continue;
				}
				LinearToolpath3<PrintVertex> path = allPaths[pi] as LinearToolpath3<PrintVertex>;

				if ( path == null )
					throw new Exception("Invalid path type!");
				if ( ! (path.Type == ToolpathTypes.Deposition || path.Type == ToolpathTypes.PlaneChange || path.Type == ToolpathTypes.Travel) )
					throw new Exception("Unknown path type!");

                // if we are travelling between two extrusion paths, and the travel distance is very short,
                // then we will skip the retract.
                bool skip_retract = false;
                if ( path.Type == ToolpathTypes.Travel && 
                     (prev_path != null && prev_path.Type == ToolpathTypes.Deposition) &&
                     (pi < N-1 && allPaths[pi+1].Type == ToolpathTypes.Deposition) ) {

                    skip_retract = (path.StartPosition.Distance(path.EndPosition) < MinRetractTravelLength);
                }
                if (EnableRetraction == false)
                    skip_retract = true;

                for ( int i = 0; i < path.VertexCount; ++i ) {
					bool last_vtx = (i == path.VertexCount-1);

					Vector3d newPos = path[i].Position;
					double newRate = path[i].FeedRate;

					if ( path.Type != ToolpathTypes.Deposition ) {

                        // [RMS] if we switched to a travel move we retract, unless we don't
                        if (skip_retract == false) {
                            if (!inRetract) {
                                curA -= FixedRetractDistance;
                                inRetract = true;
                            } 
                        }

						curPos = newPos;
						curRate = newRate;

					} else {

                        // for i == 0 this dist is always 0 !!
                        double dist = (newPos - curPos).Length;

                        if (i == 0) {
                            Util.gDevAssert(dist == 0);     // next path starts at end of previous!!
                            if (inRetract) {
                                curA += FixedRetractDistance;
                                inRetract = false;
                            }
                        } else {
                            curPos = newPos;
                            curRate = newRate;

                            double vol_scale = 1;
                            if ((path.TypeModifiers & FillTypeFlags.SupportMaterial) != 0)
                                vol_scale *= SupportExtrudeScale;

                            double feed = calculate_extrude(dist, curRate, vol_scale);
                            curA += feed;
                        }
					}

					PrintVertex v = path[i];
					v.Extrusion = GCodeUtil.Extrude(curA);
					path.UpdateVertex(i, v);

				}

                prev_path = path;
            }

			NumPaths = N;
			ExtrusionLength = curA;

		} // Calculate()


		bool is_connection(Index3i flags) {
			return (flags.a & (int)TPVertexFlags.IsConnector) != 0;
		}




		public void TestCalculation()
		{
			double curA = 0;
			Vector3d curPos = Vector3d.Zero;
			double curRate = 0;

			bool inRetract = false;

			// filter paths
			List<IToolpath> allPaths = new List<IToolpath>();
			foreach ( IToolpath ipath in Paths ) {
				ToolpathUtil.ApplyToLeafPaths(ipath, (p) => { 
					if (p is LinearToolpath3<PrintVertex> || p is ResetExtruderPathHack) { 
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

				LinearToolpath3<PrintVertex> path = allPaths[pi] as LinearToolpath3<PrintVertex>;
				if ( path == null )
					throw new Exception("Invalid path type!");

				for ( int i = 0; i < path.VertexCount; ++i ) {
					bool last_vtx = (i == path.VertexCount-1);
					    
					Vector3d newPos = path[i].Position;
					double newRate = path[i].FeedRate;

					bool send_a = false;
					string comment = "";

					if ( path.Type != ToolpathTypes.Deposition ) {
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

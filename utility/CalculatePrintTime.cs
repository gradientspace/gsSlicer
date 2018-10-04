using System;
using System.Collections.Generic;
using g3;

namespace gs
{
    /// <summary>
    /// This class calculates the extrusion and travel times for a path set.
    /// Also has ScaleExtrudeTimes(), which scales the speed of each of the
    /// extrusions, and can be used to hit a specific layer time.
    /// 
    /// (note that acceleration is not considered, so the times are a best-case scenario)
    /// 
    /// EnforceMinLayerTime() is a utility that automatically re-times layers
    /// so they take at least Settings.MinLayerTime.
    /// </summary>

    public class PrintTimeStatistics
    {
        public double ExtrudeTimeS = 0;
        public double TravelTimeS = 0;

        public double TotalTimeS
        {
            get
            {
                return ExtrudeTimeS + TravelTimeS;
            }
        }

        public void Add(PrintTimeStatistics other)
        {
            ExtrudeTimeS += other.ExtrudeTimeS;
            TravelTimeS += other.TravelTimeS;
        }

        public List<string> ToStringList()
        {
            List<string> result = new List<string>()
            {
                "TOTAL PRINT TIME ESTIMATE:",
                string.Format("        Total: {0:c}", 
                        new TimeSpan(0,0,(int)TotalTimeS)),
                string.Format("    Extrusion: {0:c}    ({1,4:##.0}%)", 
                        new TimeSpan(0,0,(int)ExtrudeTimeS), ExtrudeTimeS/TotalTimeS*100),
                string.Format("       Travel: {0:c}    ({1,4:##.0}%)", 
                        new TimeSpan(0,0,(int)TravelTimeS), TravelTimeS/TotalTimeS*100),
            };
            return result;
        }

    }


    public class CalculatePrintTime
    {
        public ToolpathSet Paths;
        public SingleMaterialFFFSettings Settings;

        // output statistics
        public PrintTimeStatistics TimeStatistics { get; private set; }


        public CalculatePrintTime(ToolpathSet paths, SingleMaterialFFFSettings settings)
        {
            Paths = paths;
            Settings = settings;
        }


        /// <summary>
        /// Calculate the extrusion and travel times
        /// </summary>
        public void Calculate()
        {
            // [TODO] could do this inline...

            // filter paths
            List<IToolpath> allPaths = new List<IToolpath>();
            foreach (IToolpath ipath in Paths) {
                ToolpathUtil.ApplyToLeafPaths(ipath, (p) => {
                    if (p is LinearToolpath3<PrintVertex> ) {
                        allPaths.Add(p);
                    }
                });
            }
            int N = allPaths.Count;

            TimeStatistics = new PrintTimeStatistics();

            for (int pi = 0; pi < N; ++pi) {
                LinearToolpath3<PrintVertex> path = allPaths[pi] as LinearToolpath3<PrintVertex>;
                if (path == null || (path.Type != ToolpathTypes.Deposition && path.Type != ToolpathTypes.Travel) )
                    continue;

                double path_time = 0;
                Vector3d curPos = path[0].Position;
                for (int i = 1; i < path.VertexCount; ++i) {
                    bool last_vtx = (i == path.VertexCount - 1);

                    Vector3d newPos = path[i].Position;
                    double newRate = path[i].FeedRate;

                    double dist = (newPos - curPos).Length;

                    double rate_mm_per_s = newRate / 60;    // feed rates are in mm/min
                    path_time += dist / rate_mm_per_s;
                    curPos = newPos;
                }
                if (path.Type == ToolpathTypes.Deposition)
                    TimeStatistics.ExtrudeTimeS += path_time;
                else
                    TimeStatistics.TravelTimeS += path_time;
            }

        } // Calculate()




        /// <summary>
        /// Scale the extrusion speeds by the given scale factor
        /// </summary>
        public void ScaleExtrudeTimes(double scaleFactor)
        {
            // filter paths
            foreach (IToolpath ipath in Paths) {
                ToolpathUtil.ApplyToLeafPaths(ipath, (p) => {
                    if (p is LinearToolpath3<PrintVertex>) {
                        LinearToolpath3<PrintVertex> path = p as LinearToolpath3<PrintVertex>;
                        if (path != null && path.Type == ToolpathTypes.Deposition) {
                            for (int i = 0; i < path.VertexCount; ++i) {
                                PrintVertex v = path[i];
                                double rate = path[i].FeedRate;
                                double scaledRate = v.FeedRate * scaleFactor;
                                if (scaledRate < Settings.MinExtrudeSpeed)
                                    scaledRate = Settings.MinExtrudeSpeed;
                                v.FeedRate = scaledRate;
                                path.UpdateVertex(i, v);
                            }
                        }
                    }
                });
            }
        }


        /// <summary>
        /// Enforce Settings.MinLayerTime on this path set
        /// Returns true if modification was required
        /// </summary>
        public bool EnforceMinLayerTime()
        {
            Calculate();
            // TODO: Check if this should compare total time or just extrusion time
            if (TimeStatistics.ExtrudeTimeS < Settings.MinLayerTime) {
                double scaleF = TimeStatistics.ExtrudeTimeS / Settings.MinLayerTime;
                if (scaleF < 0.05)
                    scaleF = 0.05;      // sanity check
                ScaleExtrudeTimes(scaleF);
                return true;
            }
            return false;
        }





    }
}

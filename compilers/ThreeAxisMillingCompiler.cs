using System;
using g3;

namespace gs
{
	// [TODO] be able to not hardcode this type?
	using LinearToolpath = LinearToolpath3<PrintVertex>;


    public interface ThreeAxisMillingCompiler : ICNCCompiler
    {
        // current tool position
        Vector3d ToolPosition { get; }

        // compiler will call this to emit status messages / etc
        Action<string> EmitMessageF { get; set; }

        void Begin();
        void AppendPaths(ToolpathSet paths, SingleMaterialFFFSettings pathSettings);
        void AppendComment(string comment);
        void End();
    }



	public class BaseThreeAxisMillingCompiler : ThreeAxisMillingCompiler
    {
        public SingleMaterialFFFSettings Settings;
        public GCodeBuilder Builder;
        public BaseMillingAssembler Assembler;

        MillingAssemblerFactoryF AssemblerF;

        /// <summary>
        /// compiler will call this to emit status messages / etc
        /// </summary>
        public virtual Action<string> EmitMessageF { get; set; }


        public BaseThreeAxisMillingCompiler(GCodeBuilder builder, SingleMaterialFFFSettings settings, MillingAssemblerFactoryF AssemblerF )
		{
			Builder = builder;
			Settings = settings;
            this.AssemblerF = AssemblerF;
		}


		public Vector3d ToolPosition {
			get { return Assembler.ToolPosition; }
		}
        public bool InRetract {
            get { return Assembler.InRetract; }
        }
        public bool InTravel {
			get { return Assembler.InTravel; }
		}

		public virtual void Begin() {
            Assembler = AssemblerF(Builder, Settings);
            Assembler.AppendComment("---BEGIN HEADER");
            Assembler.AppendHeader();
            Assembler.AppendComment("---END HEADER");
		}


		public virtual void End() {
            Assembler.UpdateProgress(100);
			Assembler.AppendFooter();
		}


        /// <summary>
        /// Compile this set of toolpaths and pass to assembler.
        /// Settings are optional, pass null to ignore
        /// </summary>
		public virtual void AppendPaths(ToolpathSet paths, SingleMaterialFFFSettings pathSettings)
        {
            SingleMaterialFFFSettings useSettings = (pathSettings == null) ? Settings : pathSettings;

            int path_index = 0;
			foreach (var gpath in paths) {
                path_index++;

                if ( IsCommandToolpath(gpath) ) {
                    ProcessCommandToolpath(gpath);
                    continue;
                }

				LinearToolpath p = gpath as LinearToolpath;

                // [RMS] this doesn't work because we are doing retract inside assembler...
				//if (p[0].Position.Distance(Assembler.ToolPosition) > 0.00001)
				//	throw new Exception("SingleMaterialFFFCompiler.AppendPaths: path " 
    //                    + path_index + ": Start of path is not same as end of previous path!");

				int i = 0;
				if ( p.Type == ToolpathTypes.Travel || p.Type == ToolpathTypes.PlaneChange ) {

                    // do retract cycle
                    if (Assembler.InRetract == false) {
                        Assembler.BeginRetract(useSettings.RetractDistanceMM, useSettings.RetractSpeed, "Retract");
                    }
                    if (Assembler.InTravel == false) {
                        Assembler.BeginTravel();
                    }

				} else if (p.Type == ToolpathTypes.Cut) {
					if (Assembler.InTravel)
                        Assembler.EndTravel();

                    if (Assembler.InRetract)
                        Assembler.EndRetract(useSettings.RetractSpeed, "End Retract");
				}

				i = 1;      // do not need to emit code for first point of path, 
							// we are already at this pos
				for (; i < p.VertexCount; ++i) {
					if (p.Type == ToolpathTypes.Travel) {
						Assembler.AppendMoveTo(p[i].Position, p[i].FeedRate, "Travel");
					} else if (p.Type == ToolpathTypes.PlaneChange) {
						Assembler.AppendMoveTo(p[i].Position, p[i].FeedRate, "Plane Change");
					} else {
						Assembler.AppendCutTo(p[i].Position, p[i].FeedRate);
					}
				}

			}

        }



        public virtual void AppendComment(string comment)
        {
            Assembler.AppendComment(comment);
        }



        /// <summary>
        /// Command toolpaths are used to pass special commands/etc to the Assembler.
        /// The positions will be ignored
        /// </summary>
        protected virtual bool IsCommandToolpath(IToolpath toolpath)
        {
            return toolpath.Type == ToolpathTypes.Custom
                || toolpath.Type == ToolpathTypes.CustomAssemblerCommands;
        }


        /// <summary>
        /// Called on toolpath if IsCommandToolpath() returns true
        /// </summary>
        protected virtual void ProcessCommandToolpath(IToolpath toolpath)
        {
            if (toolpath.Type == ToolpathTypes.CustomAssemblerCommands) {
                AssemblerCommandsToolpath assembler_path = toolpath as AssemblerCommandsToolpath;
                if (assembler_path != null && assembler_path.AssemblerF != null) {
                    assembler_path.AssemblerF(Assembler, this);
                } else {
                    emit_message("ProcessCommandToolpath: invalid " + toolpath.Type.ToString());
                }

            } else {
                emit_message("ProcessCommandToolpath: unhandled type " + toolpath.Type.ToString());
            }
            
        }



        protected virtual void emit_message(string text, params object[] args)
        {
            if (EmitMessageF != null)
                EmitMessageF(string.Format(text, args));
        }

    }
}

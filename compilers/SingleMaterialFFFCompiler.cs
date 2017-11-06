using System;
using g3;

namespace gs
{
	// [TODO] be able to not hardcode this type?
	using LinearPath = LinearPath3<PathVertex>;



    public interface ThreeAxisPrinterCompiler
    {
        // current nozzle position
        Vector3d NozzlePosition { get; }

        void Begin();
        void AppendPaths(PathSet paths);
        void End();
    }



	public class SingleMaterialFFFCompiler : ThreeAxisPrinterCompiler
	{
		SingleMaterialFFFSettings Settings;
		GCodeBuilder Builder;
        BaseDepositionAssembler Assembler;

        AssemblerFactoryF AssemblerF;

        public SingleMaterialFFFCompiler(GCodeBuilder builder, SingleMaterialFFFSettings settings, AssemblerFactoryF AssemblerF )
		{
			Builder = builder;
			Settings = settings;
            this.AssemblerF = AssemblerF;
		}


		public Vector3d NozzlePosition {
			get { return Assembler.NozzlePosition; }
		}
		public double ExtruderA {
			get { return Assembler.ExtruderA; }
		}
		public bool InRetract {
			get { return Assembler.InRetract; }
		}
		public bool InTravel {
			get { return Assembler.InTravel; }
		}

		public virtual void Begin() {
            Assembler = AssemblerF(Builder, Settings);
			Assembler.AppendHeader();
		}


		public virtual void End() {
            Assembler.UpdateProgress(100);
			Assembler.AppendFooter();
		}



		public virtual void AppendPaths(PathSet paths) {
			
			CalculateExtrusion calc = new CalculateExtrusion(paths, Settings);
			calc.Calculate(Assembler.NozzlePosition, Assembler.ExtruderA, Assembler.InRetract);


            int path_index = 0;
			foreach (var gpath in paths) {
				LinearPath p = gpath as LinearPath;
                path_index++;

				if (p[0].Position.Distance(Assembler.NozzlePosition) > 0.00001)
					throw new Exception("SingleMaterialFFFCompiler.AppendPaths: path " + path_index + ": Start of path is not same as end of previous path!");

				int i = 0;
				if ((p.Type == PathTypes.Travel || p.Type == PathTypes.PlaneChange) && Assembler.InTravel == false) {
					Assembler.DisableFan();

					// do retract cycle
					if (p[0].Extrusion.x < Assembler.ExtruderA) {
                        if (Assembler.InRetract)
                            throw new Exception("SingleMaterialFFFCompiler.AppendPaths: path " + path_index + ": already in retract!");
						Assembler.BeginRetract(p[0].Position, Settings.RetractSpeed, p[0].Extrusion.x);
					}
					Assembler.BeginTravel();

				} else if (p.Type == PathTypes.Deposition) {

					// end travel / retract if we are in that state
					if (Assembler.InTravel) {
						if (Assembler.InRetract) {
							Assembler.EndRetract(p[0].Position, Settings.RetractSpeed, p[0].Extrusion.x);
						}
						Assembler.EndTravel();
						Assembler.EnableFan();
					}
				}

				i = 1;      // do not need to emit code for first point of path, 
							// we are already at this pos

				for (; i < p.VertexCount; ++i) {
					if (p.Type == PathTypes.Travel) {
						Assembler.AppendMoveTo(p[i].Position, p[i].FeedRate, "Travel");
					} else if (p.Type == PathTypes.PlaneChange) {
						Assembler.AppendMoveTo(p[i].Position, p[i].FeedRate, "Plane Change");
					} else {
						Assembler.AppendExtrudeTo(p[i].Position, p[i].FeedRate, p[i].Extrusion.x);
					}
				}

			}

		}




	}
}

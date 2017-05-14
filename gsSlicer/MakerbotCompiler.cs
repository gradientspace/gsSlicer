using System;
using g3;

namespace gs
{
	// [TODO] be able to not hardcode this type?
	using LinearPath = LinearPath3<PathVertex>;


	// TODO: abstract this to general compiler?
	public class MakerbotCompiler
	{
		SingleMaterialFFFSettings Settings;
		GCodeBuilder Builder;
		MakerbotAssembler Assembler;

		public MakerbotCompiler(GCodeBuilder builder, SingleMaterialFFFSettings settings )
		{
			Builder = builder;
			Settings = settings;
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
			Assembler = InitializeAssembler();
			Assembler.AppendHeader();
		}

		// override to customize assembler
		protected virtual MakerbotAssembler InitializeAssembler() {
			MakerbotAssembler asm = new MakerbotAssembler(Builder, Settings as MakerbotSettings);
			return asm;
		}

		public virtual void End() {
			// final retract
			Assembler.AppendMoveToA(Assembler.NozzlePosition, Settings.RetractSpeed, 
			                        Assembler.ExtruderA - Settings.RetractDistanceMM, "Final Retract");
			Assembler.UpdateProgress(100);
			Assembler.AppendFooter();
		}



		public virtual void AppendPaths(PathSet paths) {
			
			CalculateExtrusion calc = new CalculateExtrusion(paths, Settings);
			calc.Calculate(Assembler.NozzlePosition, Assembler.ExtruderA);


			foreach (var gpath in paths) {
				LinearPath p = gpath as LinearPath;

				if (p[0].Position.Distance(Assembler.NozzlePosition) > 0.00001)
					throw new Exception("Start of path is not same as end of previous path!");

				int i = 0;
				if ((p.Type == PathTypes.Travel || p.Type == PathTypes.PlaneChange) && Assembler.InTravel == false) {
					Assembler.DisableFan();

					// do retract cycle
					if (p[0].Extrusion.x < Assembler.ExtruderA) {
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
						Assembler.AppendMoveToA(p[i].Position, p[i].FeedRate, p[i].Extrusion.x, "Extrude");
					}
				}

			}

		}




	}
}

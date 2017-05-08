using System;
using System.Diagnostics;
using g3;

namespace gs 
{
	using LinearPath = LinearPath3<PathVertex>;

	public class MakerbotTests 
	{
		public static GCodeFile OneLineTest() {

			GCodeFileAccumulator fileAccum = new GCodeFileAccumulator();
			GCodeBuilder builder = new GCodeBuilder(fileAccum);

			MakerbotSettings settings = new MakerbotSettings();

			MakerbotAssembler asm = new MakerbotAssembler(builder, settings);

			asm.AppendHeader();

			//asm.AppendMoveToA(-141, -74, 0, 1500, -1.3, "Retract");
			//asm.AppendMoveTo(-141, -74, 0, 3000, "Retract 2");
			//asm.AppendMoveTo(-141, -74, 0.2, 1380, "Layer Up");

			Vector3d startPos = new Vector3d(-141,-74,0.0);

			Vector3d pathPos = startPos;
			PathSet paths = new PathSet();

			LinearPath zup = new LinearPath(PathTypes.PlaneChange);
			zup.AppendVertex(new PathVertex(pathPos, GCodeUtil.UnspecifiedValue));
			pathPos.z += 0.2;
			zup.AppendVertex(new PathVertex(pathPos, 1380));
			paths.Append(zup);

			LinearPath travel = new LinearPath(PathTypes.Travel);
			travel.AppendVertex(new PathVertex(pathPos, GCodeUtil.UnspecifiedValue));
			pathPos = new Vector3d(-50, 0, pathPos.z);
			travel.AppendVertex(new PathVertex(pathPos, 9000));
			paths.Append(travel);

			LinearPath line = new LinearPath(PathTypes.Deposition);
			line.AppendVertex( new PathVertex(pathPos, GCodeUtil.UnspecifiedValue) );
			pathPos.x += 100;
			line.AppendVertex( new PathVertex(pathPos, 1800) );
          	paths.Append(line);


			CalculateExtruderMotion calc = new CalculateExtruderMotion(paths);
			calc.Calculate();


			bool in_retract = false;
			bool in_travel = false;
			double cur_a = 0.0f;
			Vector3d cur_pos = startPos;
			foreach ( var gpath in paths ) {
				LinearPath p = gpath as LinearPath;

				if ( p[0].Position.Distance(cur_pos) > 0.00001 )
					throw new Exception("Start of path is not same as end of previous path!");

				int i = 0;
				if ( (p.Type == PathTypes.Travel || p.Type == PathTypes.PlaneChange) && in_travel == false ) {
					asm.DisableFan();

					// do retract cycle
					if ( p[0].Extrusion.x < cur_a ) {
						asm.AppendMoveToA( p[0].Position, 1500, p[0].Extrusion.x, "Retract" );
						asm.AppendMoveTo( p[0].Position, 3000, "Retract 2?");
						in_retract = true;
					}
					in_travel = true;

				} else if ( p.Type == PathTypes.Deposition ) {

					// end travel / retract if we are in that state
					if ( in_travel ) {
						if ( in_retract ) {
							asm.AppendMoveToA( p[0].Position, 1500, p[0].Extrusion.x, "Restart" );
							in_retract = false;
						}
						in_travel = false;
						asm.EnableFan();
					}
				}

				i = 1;		// do not need to emit code for first point of path, 
							// we are already at this pos

				for ( ; i < p.VertexCount; ++i ) {
					if ( p.Type == PathTypes.Travel ) {
						asm.AppendMoveTo( p[i].Position, p[i].FeedRate, "Travel");
					} else if (p.Type == PathTypes.PlaneChange) {
						asm.AppendMoveTo( p[i].Position, p[i].FeedRate, "Plane Change");
					} else {
						asm.AppendMoveToA( p[i].Position, p[i].FeedRate, p[i].Extrusion.x, "Extrude");
					}

					cur_pos = p[i].Position;
				}

			}


			// final retract
			asm.AppendMoveToA(cur_pos, 1500, line.End.Extrusion.x - 1.3, "Retract");
			asm.UpdateProgress(100);

			asm.AppendFooter();

			return fileAccum.File;
		}


	}
}

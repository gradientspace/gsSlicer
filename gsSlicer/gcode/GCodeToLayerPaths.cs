using System;
using g3;

namespace gs 
{
	using LinearPath = LinearPath3<PathVertex>;


	// we will insert these in PathSet when we are
	// instructed to reset extruder stepper
	public class ResetExtruderPathHack : SentinelPath
	{
	}


	public class GCodeToLayerPaths : IGCodeListener
	{
		public PathSet Paths;
		public IBuildLinearPath<PathVertex> ActivePath;

		public GCodeToLayerPaths() 
		{
		}


		void push_active_path() {
			if ( ActivePath != null && ActivePath.VertexCount > 0 )
				Paths.Append(ActivePath);
			ActivePath = null;
		}

		public void Begin() {
			Paths = new PathSet();
			ActivePath = new LinearPath();
		}
		public void End() {
			push_active_path();
		}


		public void BeginTravel() {
			push_active_path();

			var newPath = new LinearPath();
			newPath.Type = PathTypes.Travel;
			ActivePath = newPath;		
		}
		public void BeginDeposition() {
			push_active_path();
				
			var newPath = new LinearPath();
			newPath.Type = PathTypes.Deposition;
			ActivePath = newPath;				
		}


		public void LinearMoveToAbsolute3d(LinearMoveData move)
		{
			if ( ActivePath == null )		// only required in some weird cases...
				BeginTravel();

			// if we are doing a Z-move, convert to 3D path
			bool bZMove = (ActivePath.VertexCount > 0 && ActivePath.End.Position.z != move.position.z);
			if ( bZMove )
				ActivePath.ChangeType( PathTypes.PlaneChange );

			PathVertex vtx = new PathVertex(
				move.position, move.rate, move.extrude.x );
			
			if ( move.source != null )
				vtx.Source = move.source;

			ActivePath.AppendVertex(vtx);
		}


		public void CustomCommand(int code, object o) {
			if ( code == (int)CustomListenerCommands.ResetExtruder ) {
				push_active_path();
				Paths.Append( new ResetExtruderPathHack() );
			}
		}



		public void LinearMoveToRelative3d(LinearMoveData move)
		{
			throw new NotImplementedException();
		}

		public void LinearMoveToAbsolute2d(LinearMoveData move) {
			throw new NotImplementedException();
		}

		public void LinearMoveToRelative2d(LinearMoveData move) {
			throw new NotImplementedException();
		}


		public void ArcToRelative2d( Vector2d pos, double radius, bool clockwise, double rate = 0 ) {
			throw new NotImplementedException();
		}

	}
}

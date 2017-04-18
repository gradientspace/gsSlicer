using System;
using g3;

namespace gs 
{
	using LinearPath = LinearPath3<PathVertex>;

	public class GCodeToLayerPaths : IGCodeListener
	{
		public PathSet Paths;
		public IBuildLinearPath<PathVertex> ActivePath;

		public GCodeToLayerPaths() 
		{
		}


		public void Begin() {
			Paths = new PathSet();
			ActivePath = new LinearPath();
		}
		public void End() {
			if (ActivePath.VertexCount > 0 )
				Paths.Append(ActivePath);
		}


		public void BeginTravel() {
			var newPath = new LinearPath();
			newPath.Type = PathTypes.Travel;
			Paths.Append(ActivePath);
			ActivePath = newPath;		
		}
		public void BeginDeposition() {
			var newPath = new LinearPath();
			newPath.Type = PathTypes.Deposition;
			Paths.Append(ActivePath);
			ActivePath = newPath;				
		}


		public void LinearMoveToAbsolute3d(Vector3d v, double rate = 0)
		{
			// if we are doing a Z-move, convert to 3D path
			bool bZMove = (ActivePath.VertexCount > 0 && ActivePath.End.Position.z != v.z);
			if ( bZMove )
				ActivePath.ChangeType( PathTypes.PlaneChange );

			ActivePath.AppendVertex( new PathVertex(v,rate) );
		}



		public void LinearMoveToRelative3d(Vector3d v, double rate = 0)
		{
			throw new NotImplementedException();
		}

		public void LinearMoveToAbsolute2d( Vector2d v, double rate = 0 ) {
			throw new NotImplementedException();
		}

		public void LinearMoveToRelative2d( Vector2d v, double rate = 0 ) {
			throw new NotImplementedException();
		}


		public void ArcToRelative2d( Vector2d pos, double radius, bool clockwise, double rate = 0 ) {
			throw new NotImplementedException();
		}

	}
}

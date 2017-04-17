using System;
using g3;

namespace gs 
{
	public class GCodeToLayerPaths : IGCodeListener
	{
		public PathSet Paths;
		public IBuildLinearPath ActivePath;

		public GCodeToLayerPaths() 
		{
		}


		public void Begin() {
			Paths = new PathSet();
			ActivePath = new LinearPath2();
		}
		public void End() {
			if (ActivePath.VertexCount > 0 )
				Paths.Append(ActivePath);
		}


		public void BeginTravel() {
			LinearPath2 newPath = new LinearPath2();
			newPath.Type = PathTypes.Travel;
			Paths.Append(ActivePath);
			ActivePath = newPath;		
		}
		public void BeginDeposition() {
			LinearPath2 newPath = new LinearPath2();
			newPath.Type = PathTypes.Deposition;
			Paths.Append(ActivePath);
			ActivePath = newPath;				
		}


		public void LinearMoveToAbsolute3d(Vector3d v)
		{
			// if we are doing a Z-move, convert to 3D path
			bool bZMove = (ActivePath.VertexCount > 0 && ActivePath.LastVertex.z != v.z);
			if ( bZMove && ActivePath is LinearPath2 )
				ActivePath = new LinearPath3(ActivePath);
			if ( bZMove )
				ActivePath.ChangeType( PathTypes.PlaneChange );

			ActivePath.AppendVertex(v);
		}



		public void LinearMoveToRelative3d(Vector3d v)
		{
			throw new NotImplementedException();
		}

		public void LinearMoveToAbsolute2d( Vector2d v ) {
			throw new NotImplementedException();
		}

		public void LinearMoveToRelative2d( Vector2d v ) {
			throw new NotImplementedException();
		}


		public void ArcToRelative2d( Vector2d pos, double radius, bool clockwise ) {
			throw new NotImplementedException();
		}

	}
}

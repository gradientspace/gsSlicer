using System;
using System.Collections;
using System.Collections.Generic;
using g3;

namespace gs 
{
	public class LinearPath3  : IBuildLinearPath
	{
		public PolyLine3d Path;
		PathTypes _pathtype;	// access via Type property

		public LinearPath3()
		{
			Path = new PolyLine3d();
			_pathtype = PathTypes.Travel;
		}
		public LinearPath3(ILinearPath copy) {
			Path = new PolyLine3d();
			_pathtype = copy.Type;		
			foreach ( Vector3d v in copy )
				Path.AppendVertex(v);
		}

		public bool IsLinear {
			get { return true; }
		}

		public bool IsPlanar {
			get { 
				double z = Path[0].z;
				for ( int i = 1; i < Path.Vertices.Count; ++i ) {
					if ( Path.Vertices[i].z != z )
						return false;
				}
				return true;
			}
		}

		public PathTypes Type {
			get { return _pathtype; }
			set { _pathtype = value; }
		}

		public AxisAlignedBox3d Bounds { 
			get {
				return Path.GetBounds();
			}
		}


		public IEnumerator<Vector3d> GetEnumerator() {
			return Path.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return Path.GetEnumerator();
		}


		public int VertexCount {
			get { return Path.VertexCount; }
		}
		public void AppendVertex(Vector3d v) {
			if ( Path.VertexCount == 0 || Path.End.DistanceSquared(v) > MathUtil.Epsilon )	
				Path.AppendVertex(v);
		}
		public Vector3d LastVertex { 
			get { return Path.End; }
		}
		public void ChangeType(PathTypes type) {
			Type = type;
		}
	}
}

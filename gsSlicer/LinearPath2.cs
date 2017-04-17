using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using g3;

namespace gs 
{
	public class LinearPath2  : IBuildLinearPath
	{
		public PolyLine2d Path;
		public double Z;
		PathTypes _pathtype;	// access via Type property

		public LinearPath2()
		{
			Path = new PolyLine2d();
			Z = 0;
			_pathtype = PathTypes.Travel;
		}

		public bool IsLinear {
			get { return true; }
		}

		public bool IsPlanar {
			get { return true; }
		}

		public PathTypes Type {
			get { return _pathtype; }
			set { _pathtype = value; }
		}

		public AxisAlignedBox3d Bounds { 
			get {
				AxisAlignedBox3d bounds = AxisAlignedBox3d.Empty;
				bounds.Min[2] = bounds.Max[2] = Z;
				foreach (Vector2d v in Path) {
					if ( v.x < bounds.Min.x ) bounds.Min.x = v.x;
					if ( v.y < bounds.Min.y ) bounds.Min.y = v.y;
					if ( v.x > bounds.Max.x ) bounds.Max.x = v.x;
					if ( v.y > bounds.Max.y ) bounds.Max.y = v.y;
				}
				return bounds;
			}
		}


		public IEnumerator<Vector3d> GetEnumerator() {
			foreach ( Vector2d v in Path )
				yield return new Vector3d(v.x, v.y, Z);
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return this.GetEnumerator();
		}


		public int VertexCount {
			get { return Path.VertexCount; }
		}
		public void AppendVertex(Vector2d v) {
			if ( Path.VertexCount == 0 || Path.End.DistanceSquared(v) > MathUtil.Epsilon )	
				Path.AppendVertex(v);
		}
		public void AppendVertex(Vector3d v) {
			if ( Path.VertexCount > 0 )
				Debug.Assert(v.z == Z);
			Z = v.z;
			AppendVertex(v.xy);
		}
		public Vector3d LastVertex { 
			get { return new Vector3d(Path.End.x, Path.End.y, Z); }
		}
		public void ChangeType(PathTypes type) {
			Type = type;
		}
	}
}

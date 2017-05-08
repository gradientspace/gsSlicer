using System;
using System.Collections.Generic;

using g3;

namespace gs 
{
	public enum PathTypes {
		Deposition,
		Travel,
		PlaneChange,

		Composite,
		Custom
	};
		

	public interface IPathVertex {
		Vector3d Position { get; set; }
	}

	public struct PathVertex : IPathVertex 
	{
		public Vector3d Position { get; set; }
		public double FeedRate;
		public Vector3d Extrusion {get; set; }

		public object Source { get; set; }

		public PathVertex(Vector3d pos, double rate) {
			Position = pos;
			FeedRate = rate;
			Extrusion = Vector3d.Zero;
			Source = null;
		}

		public PathVertex(Vector3d pos, double rate, double ExtruderA) {
			Position = pos;
			FeedRate = rate;
			Extrusion = new Vector3d(ExtruderA, 0, 0);
			Source = null;
		}

		public static implicit operator Vector3d(PathVertex v)
		{
			return v.Position;
		}
	};


	public interface IPath
	{
		PathTypes Type { get; }
		bool IsPlanar { get; }
		bool IsLinear { get; }

		AxisAlignedBox3d Bounds { get; }
	}

	public interface ILinearPath<T> : IPath, IEnumerable<T>
	{
		T this[int key] { get; }
	}

	public interface IBuildLinearPath<T> : ILinearPath<T>
	{
		void ChangeType(PathTypes type);
		void AppendVertex(T v);	
		void UpdateVertex(int i, T v);
		
		int VertexCount { get; }
		T Start { get; }
		T End { get; }
	}


	public interface IPathSet : IPath, IEnumerable<IPath>
	{
	}



	// Just a utility class we can subclass to create custom "marker" paths
	// in the path stream.
	public class SentinelPath : IPath
	{
		public PathTypes Type { 
			get {
				return PathTypes.Custom;
			}
		}
		public virtual bool IsPlanar { 
			get {
				return false;
			}
		}
		public virtual bool IsLinear { 
			get {
				return false;
			}
		}

		public virtual AxisAlignedBox3d Bounds { 
			get {
				return AxisAlignedBox3d.Zero;
			}
		}
	}

}

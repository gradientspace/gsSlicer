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

		public PathVertex(Vector3d pos, double rate) {
			Position = pos;
			FeedRate = rate;
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
		
		int VertexCount { get; }
		T Start { get; }
		T End { get; }
	}


	public interface IPathSet : IPath, IEnumerable<IPath>
	{
	}


}

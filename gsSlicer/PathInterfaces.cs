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
		


	public interface IPath
	{
		PathTypes Type { get; }
		bool IsPlanar { get; }
		bool IsLinear { get; }

		AxisAlignedBox3d Bounds { get; }
	}

	public interface ILinearPath : IPath, IEnumerable<Vector3d>
	{
	}

	public interface IBuildLinearPath : ILinearPath
	{
		void ChangeType(PathTypes type);
		void AppendVertex(Vector3d v);	
		
		int VertexCount { get; }
		Vector3d LastVertex { get; }
	}


	public interface IPathSet : IPath, IEnumerable<IPath>
	{
	}


}

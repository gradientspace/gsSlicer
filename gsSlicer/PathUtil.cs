using System;
using System.Collections.Generic;
using g3;

namespace gs 
{
	public static class PathUtil 
	{
		public static void ApplyToLeafPaths(IPath root, Action<IPath> LeafF) {
			if ( root is IPathSet ) {
				ApplyToLeafPaths( root as IPathSet, LeafF );
			} else {
				LeafF(root);
			}
		}
		public static void ApplyToLeafPaths(IPathSet root, Action<IPath> LeafF) {
			foreach ( var ipath in (root as IPathSet) )
				ApplyToLeafPaths(ipath, LeafF);
		}



		public static List<IPath> FlattenPaths(IPath root) {
			List<IPath> result = new List<IPath>();
			ApplyToLeafPaths(root, (p) => {
				result.Add(p);
			});
			return result;
		}


	}
}

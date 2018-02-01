using System;
using System.Collections.Generic;
using g3;

namespace gs 
{
	public static class ToolpathUtil 
	{
		public static readonly Index3i ConnectorVFlag = new Index3i((int)ToolpathVertexFlags.IsConnector, 0, 0);


		public static void ApplyToLeafPaths(IToolpath root, Action<IToolpath> LeafF) {
			if ( root is IToolpathSet ) {
				ApplyToLeafPaths( root as IToolpathSet, LeafF );
			} else {
				LeafF(root);
			}
		}
		public static void ApplyToLeafPaths(IToolpathSet root, Action<IToolpath> LeafF) {
			foreach ( var ipath in (root as IToolpathSet) )
				ApplyToLeafPaths(ipath, LeafF);
		}



		public static List<IToolpath> FlattenPaths(IToolpath root) {
			List<IToolpath> result = new List<IToolpath>();
			ApplyToLeafPaths(root, (p) => {
				result.Add(p);
			});
			return result;
		}


	}
}

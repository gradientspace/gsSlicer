using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public interface IPathsAssembler
    {
        void AppendPaths(IPathSet paths);

        // [TODO] we should replace this w/ a separte assembler/builder, even if the assembler is trivial!!
        PathSet TempGetAssembledPaths();
    }


    public class GenericPathsAssembler : IPathsAssembler
    {
        public PathSet AccumulatedPaths;


        public GenericPathsAssembler()
        {
            AccumulatedPaths = new PathSet();
        }


        public void AppendPaths(IPathSet paths)
        {
            AccumulatedPaths.Append(paths);
        }


        public PathSet TempGetAssembledPaths()
        {
            return AccumulatedPaths;
        }
    }
}

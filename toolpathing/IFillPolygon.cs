using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	public interface IFillPolygon
	{
		GeneralPolygon2d Polygon { get; }
		bool Compute();
	}


    public interface IPathsFillPolygon : IFillPolygon
    {
        List<FillPaths2d> GetFillPaths();
    }

    public interface IShellsFillPolygon : IPathsFillPolygon
    {
        List<GeneralPolygon2d> GetInnerPolygons();
    }

}

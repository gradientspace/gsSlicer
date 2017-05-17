using System;
using g3;

namespace gs
{
	public interface IFillPolygon
	{
		GeneralPolygon2d Polygon { get; }
		bool Compute();
	}
}

using System;
using SkiaSharp;
using g3;
using gs;


namespace SliceViewer 
{
	public static class SkiaUtil 
	{
		public static SKColorType ColorType() {
			return Util.IsRunningOnMono() ? SKColorType.Rgba8888 : SKColorType.Bgra8888;
		}

		public static SKColor Color(SKColor c, byte new_a)
		{
			return new SKColor(c.Red, c.Green, c.Blue, new_a);
		}

		public static SKColor Color(byte r, byte g, byte b, byte a = 255) {
			if ( Util.IsRunningOnMono() ) {
				return new SKColor(r, g, b, a);
			} else {
				return new SKColor(b, g, r, a);
			}
		}


		public static SKColor Blend(SKColor c0, SKColor c1, float t)
		{
			return new SKColor(
				(byte)MathUtil.Clamp(((1 - t) * c0.Red + (t) * c1.Red), 0, 255),
				(byte)MathUtil.Clamp(((1 - t) * c0.Green + (t) * c1.Green), 0, 255),
				(byte)MathUtil.Clamp(((1 - t) * c0.Blue + (t) * c1.Blue), 0, 255),
				(byte)MathUtil.Clamp(((1 - t) * c0.Alpha + (t) * c1.Alpha), 0, 255));
		}


		public static SKPath ToSKPath(GeneralPolygon2d g, Func<Vector2d, SKPoint> mapF)
		{
			SKPath p = new SKPath();

			int N = g.Outer.VertexCount;
			p.MoveTo(mapF(g.Outer[0]));
			for (int i = 1; i < N; i++)
				p.LineTo(mapF(g.Outer[i]));
			p.Close();

			foreach (Polygon2d h in g.Holes) {
				int hN = h.VertexCount;
				p.MoveTo(mapF(h[0]));
				for (int i = 1; i < hN; ++i)
					p.LineTo(mapF(h[i]));
				p.Close();
			}

			return p;
		}



	}
}

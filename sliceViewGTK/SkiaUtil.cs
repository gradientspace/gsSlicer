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

	}
}

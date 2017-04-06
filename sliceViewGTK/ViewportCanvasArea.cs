using System;
using System.Collections.Generic;
using Gtk;
using GLib;
using SkiaSharp;
using g3;
using gs;

namespace SliceViewer 
{
	class SliceViewCanvas : DrawingArea
	{
        public List<PathSet2> Paths = new List<PathSet2>();


		public bool ShowOpenEndpoints = true;


		public SliceViewCanvas() 
		{
			ExposeEvent += OnExpose;
		}


        SKPath MakePath(PolyLine2d polyLine, Func<Vector2d, SKPoint> mapF)
        {
			SKPath p = new SKPath();

            p.MoveTo(mapF(polyLine[0]));
			for ( int i = 1; i < polyLine.VertexCount; i++ )
				p.LineTo( mapF(polyLine[i]) );

            return p;
        }
        

		void OnExpose(object sender, ExposeEventArgs args)
		{
			DrawingArea area = (DrawingArea) sender;
			Cairo.Context cr =  Gdk.CairoHelper.Create(area.GdkWindow);

			int width = area.Allocation.Width;
			int height = area.Allocation.Height;
			int nBorder = 5;

            AxisAlignedBox2d bounds = new AxisAlignedBox2d(0, 0, 500, 500);

            bounds.Expand(25);
			double sx = (double)(width-2*nBorder) / bounds.Width;
			double sy = (double)(height-2*nBorder) / bounds.Height;

			float scale = (float)Math.Min(sx, sy);
			Vector2f translate = (Vector2f)(-bounds.Min);

            SKColorType useColorType = Util.IsRunningOnMono() ? SKColorType.Rgba8888 : SKColorType.Bgra8888;

			using (var bitmap = new SKBitmap(width, height, useColorType, SKAlphaType.Premul))
			{
				IntPtr len;
				using (var skSurface = SKSurface.Create(bitmap.Info.Width, bitmap.Info.Height, useColorType, SKAlphaType.Premul, bitmap.GetPixels(out len), bitmap.Info.RowBytes))
				{
					var canvas = skSurface.Canvas;
					canvas.Clear(new SKColor(240, 240, 240, 255));

					Func<Vector2d, Vector2f> xformF = (pOrig) => {
						Vector2f pNew = (Vector2f)pOrig;
						pNew += translate;
						pNew = scale * pNew;
						pNew += new Vector2f(nBorder, nBorder);
						pNew.y = canvas.ClipBounds.Height - pNew.y;
						return pNew;
					};
					Func<Vector2d, SKPoint> mapToSkiaF = (pOrig) => {
						Vector2f p = xformF(pOrig);
						return new SKPoint(p.x, p.y);
					};


					using (var paint = new SKPaint())
					{
						paint.StrokeWidth = 1;
                        SKColor pathColor = new SKColor(0, 0, 0, 255);
                        SKColor startColor = new SKColor(255, 0, 0, 128);
						float pointR = 3f;
						paint.IsAntialias = true;

						//paint.Style = SKPaintStyle.Fill;
                        paint.Style = SKPaintStyle.Stroke;

                        foreach ( PathSet2 Path in Paths ) {
                            foreach ( PolyLine2d poly in Path.Paths ) {
							    SKPath path = MakePath(poly, mapToSkiaF);
                                paint.Color = pathColor;
                                paint.StrokeWidth = 2;
                                canvas.DrawPath(path, paint);

                                paint.Color = startColor;
                                Vector2f pt = xformF(poly.Start);
								canvas.DrawCircle(pt.x, pt.y, pointR, paint);
                            }
                        }


					}

					Cairo.Surface surface = new Cairo.ImageSurface(
						bitmap.GetPixels(out len),
						Cairo.Format.Argb32,
						bitmap.Width, bitmap.Height,
						bitmap.Width * 4);


					surface.MarkDirty();
					cr.SetSourceSurface(surface, 0, 0);
					cr.Paint();
				}
			}

			//return true;
		}
	}
}

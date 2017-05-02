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
		public PathSet Paths = new PathSet();


		public bool ShowOpenEndpoints = true;

		public float Zoom = 0.95f;

		// this is a pixel-space translate
		public Vector2f Translate = Vector2f.Zero;


		public SliceViewCanvas() 
		{
			ExposeEvent += OnExpose;

			ButtonPressEvent += OnButtonPressEvent;
			ButtonReleaseEvent += OnButtonReleaseEvent;
			MotionNotifyEvent += OnMotionNotifyEvent;
			ScrollEvent += OnScrollEvent;
			Events = Gdk.EventMask.ExposureMask | Gdk.EventMask.LeaveNotifyMask |
			            Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask | Gdk.EventMask.PointerMotionMask |
			            Gdk.EventMask.ScrollMask;
		}


		// zoom
		private void OnScrollEvent(object o, ScrollEventArgs args)
		{
			if (args.Event.Direction == Gdk.ScrollDirection.Up)
				Zoom *= 1.05f;
			else
				Zoom = Math.Max( 0.25f, Zoom * (1.0f / 1.05f) );
			QueueDraw();
		}


		// pan support
		bool left_down = false;
		Vector2f start_pos = Vector2f.Zero;
		Vector2f pan_start;
		private void OnButtonPressEvent(object o, ButtonPressEventArgs args) {
			if (args.Event.Button == 1) {
				left_down = true;
				start_pos = new Vector2f((float)args.Event.X, (float)args.Event.Y);
				pan_start = Translate;
			}
		}
		private void OnButtonReleaseEvent(object o, ButtonReleaseEventArgs args) {
			if ( left_down )
				left_down = false;
		}
		private void OnMotionNotifyEvent(object o, MotionNotifyEventArgs args)
		{
			if ( left_down ) {
				Vector2f cur_pos = new Vector2f((float)args.Event.X, (float)args.Event.Y);
				Vector2f delta = cur_pos - start_pos;
				delta.y = -delta.y;
				delta *= 1.0f;  // speed
				Translate = pan_start + delta/Zoom;
				QueueDraw();
			}
		}


		void Reset()
		{
			Zoom = 1.0f;
			Translate = Vector2f.Zero;
		}







        SKPath MakePath(PolyLine2d polyLine, Func<Vector2d, SKPoint> mapF)
        {
			SKPath p = new SKPath();
            p.MoveTo(mapF(polyLine[0]));
			for ( int i = 1; i < polyLine.VertexCount; i++ )
				p.LineTo( mapF(polyLine[i]) );
            return p;
        }
		SKPath MakePath(PolyLine3d polyLine, Func<Vector2d, SKPoint> mapF)
		{
			SKPath p = new SKPath();
			p.MoveTo(mapF(polyLine[0].xy));
			for ( int i = 1; i < polyLine.VertexCount; i++ )
				p.LineTo( mapF(polyLine[i].xy) );
			return p;
		}     
		SKPath MakePath<T>(LinearPath3<T> path, Func<Vector2d, SKPoint> mapF) where T : IPathVertex
		{
			SKPath p = new SKPath();
			p.MoveTo(mapF(path[0].Position.xy));
			for ( int i = 1; i < path.VertexCount; i++ )
				p.LineTo( mapF(path[i].Position.xy) );
			return p;
		} 




		void OnExpose(object sender, ExposeEventArgs args)
		{
			DrawingArea area = (DrawingArea) sender;
			Cairo.Context cr =  Gdk.CairoHelper.Create(area.GdkWindow);

			int width = area.Allocation.Width;
			int height = area.Allocation.Height;

			//AxisAlignedBox3d bounds3 = Paths.Bounds;
			AxisAlignedBox3d bounds3 = Paths.ExtrudeBounds;
			AxisAlignedBox2d bounds = (bounds3 == AxisAlignedBox3d.Empty) ?
				new AxisAlignedBox2d(0, 0, 500, 500) : 
				new AxisAlignedBox2d(bounds3.Min.x, bounds3.Min.y, bounds3.Max.x, bounds3.Max.y );

			double sx = (double)width / bounds.Width;
			double sy = (double)height / bounds.Height;

			float scale = (float)Math.Min(sx, sy);

			// we apply this translate after scaling to pixel coords
			Vector2f pixC = Zoom * scale * (Vector2f)bounds.Center;
			Vector2f translate = new Vector2f(width/2, height/2) - pixC;

			using (var bitmap = new SKBitmap(width, height, SkiaUtil.ColorType(), SKAlphaType.Premul))
			{
				IntPtr len;
				using (var skSurface = SKSurface.Create(bitmap.Info.Width, bitmap.Info.Height, SkiaUtil.ColorType(), SKAlphaType.Premul, bitmap.GetPixels(out len), bitmap.Info.RowBytes))
				{
					var canvas = skSurface.Canvas;
					canvas.Clear(SkiaUtil.Color(240, 240, 240, 255));

					Func<Vector2d, Vector2f> xformF = (pOrig) => {
						Vector2f pNew = (Vector2f)pOrig;
						pNew -= (Vector2f)bounds.Center;
						pNew = Zoom * scale * pNew;
						pNew += (Vector2f)pixC;
						pNew += translate + Zoom*Translate;
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
						SKColor extrudeColor = SkiaUtil.Color(0, 0, 0, 255);
						SKColor travelColor = SkiaUtil.Color(0,255,0,128);
						SKColor startColor = SkiaUtil.Color(255, 0, 0, 128);
						SKColor planeColor = SkiaUtil.Color(0,0,255, 128);
						float pointR = 3f;
						paint.IsAntialias = true;

						//paint.Style = SKPaintStyle.Fill;
                        paint.Style = SKPaintStyle.Stroke;

						Action<LinearPath3<PathVertex>> drawPath3F = (polyPath) => {
							SKPath path = MakePath(polyPath, mapToSkiaF);
							if ( polyPath.Type == PathTypes.Deposition ) {
								paint.Color = extrudeColor;
							} else if ( polyPath.Type == PathTypes.Travel ) {
								paint.Color = travelColor;
								paint.StrokeWidth = 3;
							} else if ( polyPath.Type == PathTypes.PlaneChange ) {
								paint.Color = planeColor;
							} else {
								paint.Color = startColor;
							}
							canvas.DrawPath(path, paint);

							paint.StrokeWidth = 1;

							Vector2f pt = xformF(polyPath.Start.Position.xy);
							if ( polyPath.Type == PathTypes.Deposition || polyPath.Type == PathTypes.Travel ) {
								canvas.DrawCircle(pt.x, pt.y, pointR, paint);	
							} else if ( polyPath.Type == PathTypes.PlaneChange ) {
								paint.Style = SKPaintStyle.Fill;
								canvas.DrawCircle(pt.x, pt.y, 8f, paint);
								paint.Style = SKPaintStyle.Stroke;
							}

							paint.StrokeWidth = 1;
							paint.Color = startColor;
						};
						Action<IPath> drawPath = (path) => {
							if ( path is LinearPath3<PathVertex> )
								drawPath3F(path as LinearPath3<PathVertex> );
							else
								throw new NotImplementedException();
						};
						Action<IPathSet> drawPaths = null;
						drawPaths = (paths) => {
							foreach ( IPath path in paths ) {
								if ( path is IPathSet )
									drawPaths(path as IPathSet);
								else
									drawPath(path);
							}
						};

						drawPaths(Paths);

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

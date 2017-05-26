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

			SetPaths(new PathSet());
		}

		PathSet Paths;
		LayersDetector Layers;
		int currentLayer = 0;
		Func<Vector3d, byte> LayerFilterF = (v) => { return 255; };

		public void SetPaths(PathSet paths) {
			Paths = paths;
			Layers = new LayersDetector(Paths);
			CurrentLayer = 0;
		}

		public int CurrentLayer {
			get { return currentLayer; }
			set {
				currentLayer = MathUtil.Clamp(value, 0, Layers.Layers - 1);
				Interval1d layer_zrange = Layers.GetLayerZInterval(currentLayer);
				LayerFilterF = (v) => {
					return (layer_zrange.Contains(v.z)) ? (byte)255 : (byte)0;
				};
				QueueDraw();
			}
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

							Vector3d v0 = polyPath.Start.Position;
							byte layer_alpha = LayerFilterF(v0);
							if (layer_alpha == 0)
								return;

							SKPath path = MakePath(polyPath, mapToSkiaF);
							if (polyPath.Type == PathTypes.Deposition) {
								paint.Color = extrudeColor;
							} else if (polyPath.Type == PathTypes.Travel) {
								paint.Color = travelColor;
								paint.StrokeWidth = 3;
							} else if (polyPath.Type == PathTypes.PlaneChange) {
								paint.StrokeWidth = 0.5f;
								paint.Color = planeColor;
							} else {
								paint.Color = startColor;
							}
							paint.Color = SkiaUtil.Color(paint.Color, layer_alpha);

							canvas.DrawPath(path, paint);

							paint.StrokeWidth = 1;

							Vector2f pt = xformF(polyPath.Start.Position.xy);
							if (polyPath.Type == PathTypes.Deposition) {
								canvas.DrawCircle(pt.x, pt.y, pointR, paint);
							} else if (polyPath.Type == PathTypes.Travel) {
								canvas.DrawCircle(pt.x, pt.y, pointR, paint);
							} else if (polyPath.Type == PathTypes.PlaneChange) {
								paint.Style = SKPaintStyle.Fill;
								canvas.DrawCircle(pt.x, pt.y, 4f, paint);
								paint.Style = SKPaintStyle.Stroke;
							}

							paint.StrokeWidth = 1;
							paint.Color = startColor;
						};
						Action<IPath> drawPath = (path) => {
							if ( path is LinearPath3<PathVertex> )
								drawPath3F(path as LinearPath3<PathVertex> );
							// else we might have other path type...
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







		// zoom
		private void OnScrollEvent(object o, ScrollEventArgs args)
		{
			if (args.Event.Direction == Gdk.ScrollDirection.Up)
				Zoom *= 1.05f;
			else
				Zoom = Math.Max(0.25f, Zoom * (1.0f / 1.05f));
			QueueDraw();
		}


		// pan support
		bool left_down = false;
		Vector2f start_pos = Vector2f.Zero;
		Vector2f pan_start;
		private void OnButtonPressEvent(object o, ButtonPressEventArgs args)
		{
			if (args.Event.Button == 1) {
				left_down = true;
				start_pos = new Vector2f((float)args.Event.X, (float)args.Event.Y);
				pan_start = Translate;
			}
		}
		private void OnButtonReleaseEvent(object o, ButtonReleaseEventArgs args)
		{
			if (left_down)
				left_down = false;
		}
		private void OnMotionNotifyEvent(object o, MotionNotifyEventArgs args)
		{
			if (left_down) {
				Vector2f cur_pos = new Vector2f((float)args.Event.X, (float)args.Event.Y);
				Vector2f delta = cur_pos - start_pos;
				delta.y = -delta.y;
				delta *= 1.0f;  // speed
				Translate = pan_start + delta / Zoom;
				QueueDraw();
			}
		}


		void Reset()
		{
			Zoom = 1.0f;
			Translate = Vector2f.Zero;
			QueueDraw();
		}









	}





}

using System;
using System.Collections.Generic;
using Gtk;
using GLib;
using SkiaSharp;
using g3;
using gs;

namespace DLPViewer 
{
	class DLPViewCanvas : DrawingArea
	{


		public bool ShowOpenEndpoints = true;

		public float Zoom = 0.95f;

		// this is a pixel-space translate
		public Vector2f Translate = Vector2f.Zero;


		public DLPViewCanvas() 
		{
			ExposeEvent += OnExpose;

			ButtonPressEvent += OnButtonPressEvent;
			ButtonReleaseEvent += OnButtonReleaseEvent;
			MotionNotifyEvent += OnMotionNotifyEvent;
			ScrollEvent += OnScrollEvent;
			Events = Gdk.EventMask.ExposureMask | Gdk.EventMask.LeaveNotifyMask |
			            Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask | Gdk.EventMask.PointerMotionMask |
			            Gdk.EventMask.ScrollMask;

			SetSlices(new PlanarSliceStack());
		}

		PlanarSliceStack Stack;
		int currentLayer = 0;

		public void SetSlices(PlanarSliceStack slices) {
			Stack = slices;
			CurrentLayer = 0;
			SliceImages = new List<SKBitmap>();
		}

		public int CurrentLayer {
			get { return currentLayer; }
			set {
				currentLayer = MathUtil.Clamp(value, 0, Stack.Slices.Count - 1);
				QueueDraw();
			}
		}



		List<SKBitmap> SliceImages = new List<SKBitmap>();
		float CurrentDPIMM = 1.0f;


		// compute bitmap for each slice (maybe do on demand?)
		void compute_slice_images() {

			// construct bounding box and add buffer region
			AxisAlignedBox3d bounds = Stack.Bounds;
			double bufferMM = 1;
			bounds.Expand(bufferMM);
			Vector3d center = bounds.Center;

			// will create a bitmap with this dots-per-inch
			double dpi = 300;
			double dpmm = dpi / Units.Convert(Units.Linear.Inches, Units.Linear.Millimeters);
			CurrentDPIMM = (float)dpmm;

			// pixel dimensions of image
			int width = (int)(bounds.Width * dpmm);
			int height = (int)(bounds.Height * dpmm);

			// backgroun and object colors
			SKColor bgColor = SkiaUtil.Color(0, 0, 0, 255);
			SKColor objColor = SkiaUtil.Color(255, 255, 255, 255);

			// function that maps from input coordinates to pixel space
			Func<Vector2d, SKPoint> mapToSkiaF = (origPt) => {
				origPt.y = -origPt.y; // [RMS] !!! flip Y here? if we don't do this the
									  // polylines we draw below don't match up. But maybe
									  // its the polylines that are wrong??
				origPt *= dpmm;
				origPt.x += width / 2; origPt.y += height / 2;
				return new SKPoint((float)origPt.x, (float)origPt.y);
			};

			foreach (PlanarSlice slice in Stack.Slices) {

				var bitmap = new SKBitmap(width, height, SkiaUtil.ColorType(), SKAlphaType.Premul);
				IntPtr len;
				using (var skSurface = SKSurface.Create(bitmap.Info.Width, bitmap.Info.Height, SkiaUtil.ColorType(), SKAlphaType.Premul, bitmap.GetPixels(out len), bitmap.Info.RowBytes)) {

					var canvas = skSurface.Canvas;
					canvas.Clear(bgColor);

					using (var paint = new SKPaint()) {
						paint.IsAntialias = false;
						paint.Style = SKPaintStyle.Fill;
						paint.Color = objColor;
						foreach (GeneralPolygon2d poly in slice.Solids) {
							SKPath path = SkiaUtil.ToSKPath(poly, mapToSkiaF);
							canvas.DrawPath(path, paint);
						}
					}
				}

				SliceImages.Add(bitmap);
			}


		}

		SKBitmap get_slice_image(int i) {
			if (SliceImages.Count == 0)
				compute_slice_images();
			return SliceImages[i];
		}




		void OnExpose(object sender, ExposeEventArgs args)
		{
			DrawingArea area = (DrawingArea) sender;
			Cairo.Context cr =  Gdk.CairoHelper.Create(area.GdkWindow);

			int width = area.Allocation.Width;
			int height = area.Allocation.Height;

			AxisAlignedBox3d bounds3 = Stack.Bounds;
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
						SKBitmap sliceImg = get_slice_image(currentLayer);
						float w = sliceImg.Width / CurrentDPIMM, h = sliceImg.Height / CurrentDPIMM;
						w *= Zoom * scale;
						h *= Zoom * scale;
						SKPoint sliceCenter = mapToSkiaF(Vector2d.Zero);
						SKRect drawRect = new SKRect(
							sliceCenter.X - w / 2, sliceCenter.Y - h / 2,
							sliceCenter.X + w / 2, sliceCenter.Y + h / 2);
						canvas.DrawBitmap(sliceImg, drawRect, paint); 


						paint.IsAntialias = true;
						paint.Style = SKPaintStyle.Stroke;

						PlanarSlice slice = Stack.Slices[currentLayer];

						paint.Color = SkiaUtil.Color(255, 0, 0, 255); ;
						paint.StrokeWidth = 2;
						foreach ( GeneralPolygon2d poly in slice.Solids ) {
							SKPath path = SkiaUtil.ToSKPath(poly, mapToSkiaF);
							canvas.DrawPath(path, paint);
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

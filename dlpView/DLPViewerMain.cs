using System;
using System.Collections.Generic;
using System.IO;

using Gtk;
using GLib;
using SkiaSharp;
using g3;
using gs;

namespace DLPViewer
{


	class MainClass
	{

		public static Window MainWindow;
		public static DLPViewCanvas View; 


		public static void Main(string[] args)
		{
			ExceptionManager.UnhandledException += delegate (UnhandledExceptionArgs expArgs) {
				Console.WriteLine(expArgs.ExceptionObject.ToString());
				expArgs.ExitApplication = true;
			};

			Gtk.Application.Init();

			MainWindow = new Window("DLPViewer");
			MainWindow.SetDefaultSize(900, 600);
			MainWindow.SetPosition(WindowPosition.Center);
			MainWindow.DeleteEvent += delegate {
				Gtk.Application.Quit();
			};



			DMesh3 mesh = StandardMeshReader.ReadMesh("../../../sample_files/bunny_solid_5cm_min.obj");


			MeshPlanarSlicer slicer = new MeshPlanarSlicer();
			slicer.LayerHeightMM = 0.2;
			slicer.AddMesh(mesh);
			PlanarSliceStack sliceStack = slicer.Compute();

            View = new DLPViewCanvas();
			View.SetSlices(sliceStack);
            MainWindow.Add(View);

			MainWindow.KeyReleaseEvent += Window_KeyReleaseEvent;

            MainWindow.ShowAll();

            Gtk.Application.Run();
        }

		void OnException(object o, UnhandledExceptionArgs args)
		{

		}


		private static void Window_KeyReleaseEvent(object sender, KeyReleaseEventArgs args)
		{
			if (args.Event.Key == Gdk.Key.Up) {
				if ( (args.Event.State & Gdk.ModifierType.ShiftMask) != 0 )
					View.CurrentLayer = View.CurrentLayer + 10;
				else
					View.CurrentLayer = View.CurrentLayer + 1;
			} else if (args.Event.Key == Gdk.Key.Down) {
				if ((args.Event.State & Gdk.ModifierType.ShiftMask) != 0)
					View.CurrentLayer = View.CurrentLayer - 10;
				else
					View.CurrentLayer = View.CurrentLayer - 1;
			}
		}






	}
}

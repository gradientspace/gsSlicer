using System;
using System.Collections.Generic;
using System.IO;

using Gtk;
using GLib;
using SkiaSharp;
using g3;
using gs;

namespace SliceViewer 
{


	class MainClass
	{

        
		public static void Main(string[] args)
		{
			ExceptionManager.UnhandledException += delegate(UnhandledExceptionArgs expArgs)
			{
				Console.WriteLine(expArgs.ExceptionObject.ToString());
				expArgs.ExitApplication = true;
			};

			Gtk.Application.Init();

			var window = new Window("SliceViewer");
			window.SetDefaultSize(800, 600);
			window.SetPosition(WindowPosition.Center);
			window.DeleteEvent += delegate
			{
				Gtk.Application.Quit();
			};


            PathSet2 Layer = new PathSet2();

            PolyLine2d square = new PolyLine2d();
            square.AppendVertex(new Vector2d(100, 100));
            square.AppendVertex(new Vector2d(400, 100));
            square.AppendVertex(new Vector2d(400, 400));
            square.AppendVertex(new Vector2d(100, 400));
            square.AppendVertex(new Vector2d(100, 105));

            Layer.Paths.Add(square);


            var darea = new SliceViewCanvas();
            darea.Paths.Add(Layer);
            window.Add(darea);

            window.ShowAll();

            Gtk.Application.Run();
        }

		void OnException(object o, UnhandledExceptionArgs args)
		{

		}




	}
}

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
			window.SetDefaultSize(900, 600);
			window.SetPosition(WindowPosition.Center);
			window.DeleteEvent += delegate
			{
				Gtk.Application.Quit();
			};


			string sPath = "../../../sample_files/disc_single_layer.gcode";
			//string sPath = "../../../sample_files/disc_0p6mm.gcode";

			GenericGCodeParser parser = new GenericGCodeParser();
			GCodeFile gcode;
			using (FileStream fs = new FileStream(sPath, FileMode.Open, FileAccess.Read)) {
				using (TextReader reader = new StreamReader(fs) ) {
					gcode = parser.Parse(reader);
				}
			}


			GCodeToLayerPaths converter = new GCodeToLayerPaths();
			MakerbotInterpreter interpreter = new MakerbotInterpreter();
			interpreter.AddListener(converter);

			InterpretArgs interpArgs = new InterpretArgs();
			interpreter.Interpret(gcode, interpArgs);

			PathSet Layer = converter.Paths;

			//PathSet Layer = new PathSet();
			//LinearPath2 path = new LinearPath2();
			//path.AppendVertex(new Vector2d(100, 100));
			//path.AppendVertex(new Vector2d(400, 100));
			//path.AppendVertex(new Vector2d(400, 400));
			//path.AppendVertex(new Vector2d(100, 400));
			//path.AppendVertex(new Vector2d(100, 105));
			//Layer.Append(path);


            var darea = new SliceViewCanvas();
			darea.Paths = Layer;
            window.Add(darea);

            window.ShowAll();

            Gtk.Application.Run();
        }

		void OnException(object o, UnhandledExceptionArgs args)
		{

		}




	}
}

# gsSlicer

**In-Progress** Slicer for 3D printing, and other toolpath-type things, perhaps.

C#, MIT License (*but see notes below!*). Copyright 2017 ryan schmidt / gradientspace

questions? get in touch on twitter: [@rms80](http://www.twitter.com/rms80) or [@gradientspace](http://www.twitter.com/gradientspace), 
or email [rms@gradientspace.com](mailto:rms@gradientspace.com?subject=gsSlicer).

# What is this?

gsSlicer is an in-development open-source library for things like slicing 3D triangle meshes into planar polygons, filling those polygons with contour & raster fill paths, figuring out how much material to extrude along the paths, and then outputting GCode. The included **SliceViewer** project is also a GCode viewer/utility.

The goal with this project is to create a well-structured slicing engine that is designed from the ground up to be extensible. Although the initial focus will be on FDM/FFF-style printers, many of the parts of the system should be applicable to other processes like SLA, etc. Hopefully. Fingers crossed. At least, we'll definitely solve the meshes-to-slices problem for you.

# Current Status

**Under Active Development**. There is no standalone slicing "tool" yet. Basic mesh slicing is possible but you have to edit the filename in SliceViewer code! It does contours and infill, and (just barely) roofs, but not floors, or support, or lots of other important slicing things. Has hardly been tested. Only works for a Makerbot Replicator 2. And so on!

So, unless you want to work with the code, check back later.


# Dependencies

The slicing & path planning library **gsSlicer** depends on:

* [geometry3Sharp](https://github.com/gradientspace/geometry3Sharp) Boost license, git submodule
* [gsGCode](https://github.com/gradientspace/gsGCode) MIT license, git submodule
* [Clipper](http://www.angusj.com/delphi/clipper.php) by Angus Johnson, Boost license, embedded in /gsSlicer/thirdparty/clipper_library

No GPL/LGPL involved. All the code you would need to make an .exe that slices a mesh is available for unrestricted commercial use.

The **SliceViewer** project code is also MIT-license, but it depends on Gdk/Gtk/GtkSharp (LGPL) and the SkiaSharp wrapper (MIT) around the Skia library (BSD).  


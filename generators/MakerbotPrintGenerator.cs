using System;

namespace gs
{

    public class MakerbotPrintGenerator : ThreeAxisPrintGenerator
    {
        GCodeFileAccumulator file_accumulator;
        GCodeBuilder builder;
        MakerbotCompiler compiler;

        public MakerbotPrintGenerator(PrintMeshAssembly meshes, 
                                      PlanarSliceStack slices,
                                      MakerbotSettings settings)
        {
            file_accumulator = new GCodeFileAccumulator();
            builder = new GCodeBuilder(file_accumulator);
            compiler = new MakerbotCompiler(builder, settings);

            base.Initialize(meshes, slices, settings, compiler);
        }

        protected override GCodeFile extract_result()
        {
            return file_accumulator.File;
        }
    }

}

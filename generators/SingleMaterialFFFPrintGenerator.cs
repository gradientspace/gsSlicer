using System;

namespace gs
{

    public class SingleMaterialFFFPrintGenerator : ThreeAxisPrintGenerator
    {
        GCodeFileAccumulator file_accumulator;
        GCodeBuilder builder;
        MakerbotCompiler compiler;

        public SingleMaterialFFFPrintGenerator(PrintMeshAssembly meshes, 
                                      PlanarSliceStack slices,
                                      SingleMaterialFFFSettings settings)
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

using System;

namespace gs
{

    public class SingleMaterialFFFPrintGenerator : ThreeAxisPrintGenerator
    {
        GCodeFileAccumulator file_accumulator;
        GCodeBuilder builder;
        SingleMaterialFFFCompiler compiler;

        public SingleMaterialFFFPrintGenerator(PrintMeshAssembly meshes, 
                                      PlanarSliceStack slices,
                                      SingleMaterialFFFSettings settings,
                                      AssemblerFactoryF assemblerF )
        {
            file_accumulator = new GCodeFileAccumulator();
            builder = new GCodeBuilder(file_accumulator);
            compiler = new SingleMaterialFFFCompiler(builder, settings, assemblerF);

            base.Initialize(meshes, slices, settings, compiler);
        }

        protected override GCodeFile extract_result()
        {
            return file_accumulator.File;
        }



        public static SingleMaterialFFFPrintGenerator Auto(
                PrintMeshAssembly meshes, PlanarSliceStack slices, SingleMaterialFFFSettings settings)
        {
            if (settings is MakerbotSettings) {
                return new SingleMaterialFFFPrintGenerator(meshes, slices, settings, MakerbotAssembler.Factory);
            } else if (settings is RepRapSettings) {
                return new SingleMaterialFFFPrintGenerator(meshes, slices, settings, RepRapAssembler.Factory);
            } else
                throw new NotImplementedException("SingleMaterialFFFPrintGenerator.Auto: unknown settings type");
        }
    }


}

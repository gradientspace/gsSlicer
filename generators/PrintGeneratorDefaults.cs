using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace gs
{

    /// <summary>
    /// Default implementations of "pluggable" ThreeAxisPrintGenerator functions
    /// </summary>
    public static class PrintGeneratorDefaults
    {

        /*
         * Compiler Post-Processors
         */

        public static void AppendPrintTimeStatistics(
            ThreeAxisPrinterCompiler compiler, ThreeAxisPrintGenerator printgen)
        {
            compiler.AppendComment("".PadRight(79, '-'));
            foreach (string line in printgen.TotalPrintTimeStatistics.ToStringList()) {
                compiler.AppendComment(" " + line);
            }
            compiler.AppendComment("".PadRight(79, '-'));
        }


    }
}

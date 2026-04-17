using System;

namespace DacpacToolFramework
{
    class Program
    {
        static int Main(string[] args)
        {
            // Expected invocation from MSBuild.Sdk.SqlProj:
            //   DacpacToolFramework.exe add-references -i <dacpac> -asm <dll> [-asm <dll> ...] [--debug]
            // This tool is a placeholder; the actual implementation of applying SQL CLR
            // assembly references to the dacpac is not yet available.
            Console.Error.WriteLine("DacpacToolFramework invoked: " + string.Join(" ", args ?? Array.Empty<string>()));
            Console.Error.WriteLine("DacpacToolFramework does not yet implement 'add-references'.");
            Console.Error.WriteLine("Remove <Reference> items from the project, or wait for a future release that implements this tool.");
            return 1;
        }
    }
}


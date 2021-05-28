using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace iscan
{
	class Program
	{
		static int Main(string[] args)
		{
			try
			{
				ParallelAnalyzer.ProcessCompileCommandsJson("/code/foo/compile_commands.json");
#if MEH
				if (args.Length > 0)
					a.Run(args[0]);
				else
					a.TestRun();
#endif
				return 0;
			}
			catch (Exception ex)
			{
				Log.Error("Error: " + ex.ToString());
				return 1;
			}
		}
	}
}

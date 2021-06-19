using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace iscan
{
	internal class Program
	{
		private static int Main(string[] args)
		{
			var sw = Stopwatch.StartNew();
			try
			{
				if (args.Length >= 2)
				{
					ParallelAnalyzer.ProcessCompileCommandsJson(args[0], args[1]);
				}
				else
				{
					ParallelAnalyzer.ProcessCompileCommandsJson(
						"./compile_commands.json",
						"./_iscan_out.txt");
				}
				Log.Info("Total time: " + sw.Elapsed.TotalSeconds + " seconds");
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
				Log.Info("Total time: " + sw.Elapsed.TotalSeconds + " seconds");
				return 1;
			}
		}
	}
}

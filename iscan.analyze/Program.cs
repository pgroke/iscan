using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using iscan.dm;

namespace iscan.analyze
{
	class Program
	{
		static void Main(string[] args)
		{
			var sw = Stopwatch.StartNew();
			using (var file = File.OpenRead("_iscan_out.txt"))
			{
				var project = DMReader.ReadProject(file);
				Console.WriteLine("Time: " + sw.ElapsedMilliseconds + " ms");
				Console.WriteLine("TUs: " + project.tus.Length);
				Console.WriteLine("Paths: " + project.paths.Length);
				Console.WriteLine("Memory 0: " + GC.GetTotalMemory(false) / 1024 + " k");
				for (int g = 0; g <= GC.MaxGeneration; g++)
					GC.Collect(g);
				Console.WriteLine("Memory 1: " + GC.GetTotalMemory(true) / 1024 + " k");
				GC.KeepAlive(project);
			}
		}
	}

}

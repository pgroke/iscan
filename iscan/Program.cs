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
				var a = new Analyzer();
				a.TestRun();
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

using System;
using System.Collections.Generic;
using System.Text;

namespace iscan
{
	static class Log
	{
		public static void Info(string s)
		{
			Console.Error.WriteLine(s);
		}

		public static void Warning(string s)
		{
			Console.Error.WriteLine(s);
		}

		public static void Error(string s)
		{
			Console.Error.WriteLine(s);
		}
	}
}

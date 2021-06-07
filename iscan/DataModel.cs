using System;
using System.Collections.Generic;
using System.Text;

namespace iscan
{
	class DMProject
	{
		public DMProject()
		{
			tus = new List<DMTranslationUnit>();
			paths = new List<string>();
		}

		public List<string> paths { get; set; }
		public List<DMTranslationUnit> tus { get; set; }
	}

	class DMTranslationUnit
	{
		public DMTranslationUnit()
		{
			files = new List<DMFile>();
		}

		public int path { get; set; }
		public List<DMFile> files { get; set; }
	}

	class DMFile
	{
		public DMFile()
		{
			inc = new List<int>();
		}

		public int path { get; set; }
		public int stk { get; set; }
		public int sln { get; set; }
		public List<int> inc { get; private set; }
	}
}

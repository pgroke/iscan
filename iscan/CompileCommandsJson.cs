using System;
using System.Collections.Generic;
using System.Text;

namespace iscan
{
#pragma warning disable IDE1006 // Naming Styles

	internal class CompileCommandsJson
    {
		public List<CompileCommandsJsonEntry> entries { get; set; }
	}

	internal class CompileCommandsJsonEntry
    {
        public string directory { get; set; }
        public string command { get; set; }
        public string file { get; set; }
    }

#pragma warning restore IDE1006 // Naming Styles
}

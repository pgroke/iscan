using System;
using System.Collections.Generic;
using System.Text;

namespace iscan
{
    class CompileCommandsJson
    {
        public List<CompileCommandsJsonEntry> entries { get; set; }
    }

    class CompileCommandsJsonEntry
    {
        public string directory { get; set; }
        public string command { get; set; }
        public string file { get; set; }
    }
}

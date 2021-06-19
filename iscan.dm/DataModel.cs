using System;
using System.Collections.Generic;
using System.Text;

namespace iscan.dm
{
    public class DMProject
    {
        public string[] paths { get; set; }
        public DMTranslationUnit[] tus { get; set; }
    }

    public class DMTranslationUnit
    {
        public int path { get; set; }
        public DMFile[] files { get; set; }
    }

    public class DMFile
    {
        public int path { get; set; }
        public int stk { get; set; }
        public int sln { get; set; }
        public int[] inc { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace iscan
{
    static class CommandLine
    {
        public static List<string> Split(string commandLine)
        {
            bool escaped = false;
            bool quoted = false;
            var sb = new StringBuilder();
            var args = new List<string>();

            foreach (var ch in commandLine)
            {
                if (escaped)
                {
                    escaped = false;
                    sb.Append(ch);
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    sb.Append(ch);
                    continue;
                }

                if (ch == '"')
                {
                    quoted = !quoted;
                    sb.Append(ch);
                    continue;
                }
                    
                if (ch == ' ' || ch == '\t' || ch == '\v' || ch == '\r' || ch == '\b' || ch == '\f')
                {
                    if (sb.Length > 0)
                        args.Add(sb.ToString());
                    sb.Length = 0;
                }
                else
                {
                    sb.Append(ch);
                }
            }

            if (sb.Length > 0)
                args.Add(sb.ToString());

            return args;
        }
    }
}

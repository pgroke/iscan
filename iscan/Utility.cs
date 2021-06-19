using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace iscan
{
	static internal class Utility
	{
		public static int ExecuteCommand(string commandLine, Action<string> stdoutSink, Action<string> stderrSink)
		{
			using var proc = new Process();
			proc.StartInfo.FileName = "/bin/bash";
			proc.StartInfo.Arguments = "-c " + BashEscape(commandLine);
			proc.StartInfo.RedirectStandardInput = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.StartInfo.RedirectStandardError = true;
			proc.OutputDataReceived += (object sender, DataReceivedEventArgs e) => { if (e.Data != null) stdoutSink(e.Data); };
			proc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => { if (e.Data != null) stderrSink(e.Data); };

			if (!proc.Start())
				throw new Exception("Failed to start process: " + commandLine);

			proc.StandardInput.Close();
			proc.BeginOutputReadLine();
			proc.BeginErrorReadLine();

			proc.WaitForExit();
			return proc.ExitCode;
		}

		private static string BashEscape(string str)
		{
			return "\"" + str.Replace("\"", "\\\"") + "\"";
		}
	}
}

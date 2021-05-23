using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace iscan
{
	static class Utility
	{
		public static (List<string> stdout, List<string> stderr) ExecuteCommand(string commandLine)
		{
			var stdout = new List<string>();
			var stderr = new List<string>();
			using (var proc = new Process())
			{
				proc.StartInfo.FileName = "/bin/bash";
				proc.StartInfo.Arguments = "-c " + BashEscape(commandLine);
				Console.WriteLine(proc.StartInfo.Arguments);
				proc.StartInfo.RedirectStandardInput = true;
				proc.StartInfo.RedirectStandardOutput = true;
				proc.StartInfo.RedirectStandardError = true;
				proc.OutputDataReceived += (object sender, DataReceivedEventArgs e) => { if (e.Data != null) stdout.Add(e.Data); };
				proc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => { if (e.Data != null) stderr.Add(e.Data); };

				if (!proc.Start())
					throw new Exception("Failed to start process: " + commandLine);

				proc.StandardInput.Close();
				proc.BeginOutputReadLine();
				proc.BeginErrorReadLine();

				proc.WaitForExit();
				if (proc.ExitCode != 0)
				{
					foreach (var s in stderr)
						Log.Info("stderr: " + s);
					throw new Exception("Command failed with exit code " + proc.ExitCode + ": " + commandLine);
				}
			}

			return (stdout, stderr);
		}

		private static string BashEscape(string str)
		{
			return "\"" + str.Replace("\"", "\\\"") + "\"";
		}
	}
}

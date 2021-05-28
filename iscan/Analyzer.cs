using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Threading;

namespace iscan
{
	class Analyzer
	{
		public static FileEntry AnalyzeCompileCommand(string compileCommand)
		{
			var analyzer = new Analyzer();
			analyzer.Run(compileCommand);
			return analyzer.m_mainFile;
		}

		private void Run(string compileCommand)
		{
			for (int i = 0; i < 1; i++)
			{
				(var stdout, var stderr) = Utility.ExecuteCommand(StripCommand(compileCommand) + " -E -Wp,-v");
				ProcessOutput(stdout);
			}

#if MEH
			Console.WriteLine("");
			Console.WriteLine("");
			Console.WriteLine("TOKENS:");
			int total = 0;
			var filesSorted = m_files.OrderBy(e => -e.Value.SelfTokenCount);
			foreach (var entry in filesSorted)
			{
				Console.WriteLine("    " + entry.Value.SelfTokenCount.ToString().PadRight(10) + entry.Key);
				total += entry.Value.SelfTokenCount;
			}
			Console.WriteLine("");
			Console.WriteLine("    " + total.ToString().PadRight(10) + "TOTAL");

			Console.WriteLine("");
			Console.WriteLine("");
			Console.WriteLine("LINES:");
			total = 0;
			filesSorted = m_files.OrderBy(e => -e.Value.SelfLineCount);
			foreach (var entry in filesSorted)
			{
				Console.WriteLine("    " + entry.Value.SelfLineCount.ToString().PadRight(10) + entry.Key);
				total += entry.Value.SelfLineCount;
			}
			Console.WriteLine("");
			Console.WriteLine("    " + total.ToString().PadRight(10) + "TOTAL");
#endif

#if MEH
			Console.WriteLine("");
			Console.WriteLine("** stderr **");
			foreach (var s in stderr)
				Console.WriteLine(s);
#endif
		}

		private string StripCommand(string command)
		{
			var parts = CommandLine.Split(command);
			var sb = new StringBuilder();
			sb.Append(parts[0]);

			bool skipNext = false;
			foreach (var p in parts.Skip(1))
			{
				if (skipNext)
				{
					skipNext = false;
					continue;
				}
				if (p == "-o" || p == "\"-o\"")
				{
					skipNext = true;
					continue;
				}
				if (p == "-c" || p == "\"-c\"")
					continue;

				sb.Append(" ");
				sb.Append(p);
			}

			return sb.ToString();
		}

		private void ProcessOutput(List<string> ppout)
		{
			foreach (var line in ppout)
			{
				if (line.StartsWith('#'))
				{
					//Console.WriteLine(line);

					if (line.Length < 2)
						continue;
					if (line[1] != ' ')
						continue;
					var pathStart = line.IndexOf('"');
					if (pathStart < 0)
					{
						Log.Warning("Cannot parse line info line: " + line);
						continue;
					}

					var pathEnd = line.IndexOf('"', pathStart + 1);
					if (pathEnd < 0)
					{
						Log.Warning("Cannot parse line info line: " + line);
						continue;
					}

					var lineInfo = new LineInfo();
					lineInfo.path = line.Substring(pathStart + 1, pathEnd - pathStart - 1);

					var flags0 = line.Substring(pathEnd + 1);
					var flags1 = flags0.Split(" ", StringSplitOptions.RemoveEmptyEntries);

					foreach (var f in flags1)
					{
						switch (f)
						{
							case "1":
								lineInfo.fileStart = true;
								break;
							case "2":
								lineInfo.fileReturn = true;
								break;
							case "3":
								lineInfo.systemHeader = true;
								break;
							case "4":
								lineInfo.externCBlock = true;
								break;
						}
					}

					m_currentLine = line;
					ProcessLineInfoLine(lineInfo);
				}
				else
				{
					ProcessContentLine(line);
				}
			}

			m_currentLine = null;

			if (m_includeStack.Count != 1)
				throw new Exception("Missing return from include: EOF.");
			Debug.Assert(m_includeStack[0] == m_mainFile);

			LeaveFile(true);
		}

		private void ProcessLineInfoLine(LineInfo lineInfo)
		{
			if (lineInfo.fileStart && lineInfo.fileReturn)
				throw new Exception("File start and return at the same time: " + m_currentLine);

			var fileEntry = GetFileEntry(lineInfo.path, m_mainFile == null);

			// Workaround: start of main file is missing start flag
			if (m_mainFile == null && m_includeStack.Count == 0 && !lineInfo.fileReturn)
			{
				lineInfo.fileStart = true;
			}
			// Workaround: start of <built-in> is missing start flag
			else if (!m_startBuiltInWorkaroundDone && lineInfo.path == "<built-in>" && !lineInfo.fileReturn)
			{
				m_startBuiltInWorkaroundDone = true;
				lineInfo.fileStart = true;
			}
			// Workaround: start of <command-line> is missing start flag and implies missing return from <built-in>
			else if (!m_startCommandLineWorkaroundDone && lineInfo.path == "<command-line>" && !lineInfo.fileReturn && m_includeStack.Count == 2 && m_includeStack[1].Path == "<built-in>")
			{
				m_startCommandLineWorkaroundDone = true;
				lineInfo.fileStart = true;
				m_includeStack.RemoveAt(1);
			}
			// Workaround: return from <command-line> to main file is missing return flag
			else if (!m_returnToMainWorkaroundDone && m_includeStack.Count == 2 && m_includeStack[1].Path == "<command-line>" && fileEntry == m_includeStack[0])
			{
				m_returnToMainWorkaroundDone = true;
				lineInfo.fileReturn = true;
			}

			if (m_mainFile == null)
				m_mainFile = fileEntry;

			if (lineInfo.fileStart)
			{
				EnterFile(fileEntry);
			}
			else if (lineInfo.fileReturn)
			{
				LeaveFile(false);
			}

			if (fileEntry != CurrentFile())
			{
				// Workaround: output sometimes contains just a directory name before the "<built-in>" line - ignore
				if (!m_startBuiltInWorkaroundDone && !lineInfo.HasAnyFlag())
				{
					// ingore
				}
				else
					throw new Exception("Meh: " + m_currentLine);
			}
		}

		private void EnterFile(FileEntry fileEntry)
		{
			if (m_includeStack.Count > 0)
			{
				CountContents();
				CurrentFile().AddInclude(fileEntry);
			}

			m_includeStack.Add(fileEntry);
			//PrintStack();
		}

		private void LeaveFile(bool finalLeaveMainFile)
		{
			CountContents();
			if (m_includeStack.Count < 2 && !finalLeaveMainFile)
				throw new Exception("Leaving last file: " + m_currentLine);
			m_includeStack.RemoveAt(m_includeStack.Count - 1);
			//PrintStack();
		}

		private void CountContents()
		{
			int tokenCount = TokenCounter.CountTokens(m_currentContents);

			int lineCount = 0;
			foreach (var line in m_currentContents)
			{
				foreach (char ch in line)
				{
					if (ch != ' ')
					{
						lineCount++;
						break;
					}
				}
			}

			CurrentFile().SelfTokenCount += tokenCount;
			CurrentFile().SelfLineCount += lineCount;

			m_currentContents.Clear();
		}

		private FileEntry GetFileEntry(string path, bool isMainFile)
		{
			var fileEntry = m_files.GetValueOrDefault(path, null);
			if (fileEntry == null)
			{
				fileEntry = new FileEntry(path, isMainFile);
				m_files.Add(path, fileEntry);
			}
			return fileEntry;
		}

		private void ProcessContentLine(string line)
		{
			m_currentContents.Add(line);
		}

		private FileEntry CurrentFile()
		{
			return m_includeStack[m_includeStack.Count - 1];
		}

		private string m_currentLine = null;
		private FileEntry m_mainFile = null;
		private bool m_startBuiltInWorkaroundDone = false;
		private bool m_startCommandLineWorkaroundDone = false;
		private bool m_returnToMainWorkaroundDone = false;

		private List<FileEntry> m_includeStack = new List<FileEntry>();
		private List<string> m_currentContents = new List<string>();

		private Dictionary<string, FileEntry> m_files = new Dictionary<string, FileEntry>();
	}

	class FileEntry
	{
		public FileEntry(string path, bool isTU)
		{
			Path = path;
			IsTranslationUnit = isTU;
			Includes = new Dictionary<FileEntry, bool>();
		}

		public void AddInclude(FileEntry include)
		{
			if (!Includes.ContainsKey(include))
				Includes.Add(include, true);
		}

		public readonly string Path;
		public readonly bool IsTranslationUnit;
		public readonly Dictionary<FileEntry, bool> Includes;
		public int SelfTokenCount = 0;
		public int SelfLineCount = 0;
	}

	struct LineInfo
	{
		public string path;

		public bool fileStart;
		public bool fileReturn;
		public bool systemHeader;
		public bool externCBlock;

		public bool HasAnyFlag()
		{
			return fileStart || fileReturn || systemHeader || externCBlock;
		}
	}

	class ParallelAnalyzer
	{
		public static void ProcessCompileCommandsJson(string compileCommandsJson)
		{
			var pa = new ParallelAnalyzer();
			pa.Run(compileCommandsJson);
		}

		private void Run(string compileCommandsJson)
		{
			var json = File.ReadAllText(compileCommandsJson);
			var compileCommands = JsonSerializer.Deserialize<CompileCommandsJson>("{ \"entries\": " + json + " }");

			foreach (var entry in compileCommands.entries)
			{
				m_jobList.AddLast(entry);
				if (m_jobList.Count >= 100)
					break;
			}
			m_totalJobCount = m_jobList.Count;

			var threads = new List<Thread>();
			for (int i = 0; i < 24; i++)
				threads.Add(new Thread(ThreadFunction));
			foreach (var t in threads)
				t.Start();
			foreach (var t in threads)
				t.Join();
		}

		private void ThreadFunction()
		{
			while (true)
			{
				int fileNum;
				CompileCommandsJsonEntry file;

				lock (m_jobListMutex)
				{
					if (m_jobList.Count == 0)
						break;
					file = m_jobList.First.Value;
					m_jobList.RemoveFirst();
					fileNum = m_totalJobCount - m_jobList.Count;
				}

				if (File.Exists(file.file))
				{
					Console.WriteLine("{0}/{1} '{2}'...", fileNum, m_totalJobCount, file.file);
					Analyzer.AnalyzeCompileCommand(file.command);
				}
				else
					Console.WriteLine("Skipping non-existant file '" + file.file + "'.");
			}
		}

		private readonly object m_jobListMutex = new object();
		private readonly LinkedList<CompileCommandsJsonEntry> m_jobList = new LinkedList<CompileCommandsJsonEntry>();
		private int m_totalJobCount;
	}
}

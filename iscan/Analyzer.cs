using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Threading;
using iscan.dm;

namespace iscan
{
	class Analyzer
	{
		public static FileEntry AnalyzeCompileCommand(string compileCommand, SynchronizedStringMap stringMap)
		{
			var analyzer = new Analyzer(stringMap);
			if (!analyzer.Run(compileCommand))
				return null;
			return analyzer.m_mainFile;
		}

		private Analyzer(SynchronizedStringMap stringMap)
		{
			m_stringMap = stringMap;
		}

		private bool Run(string compileCommand)
		{
			var strippedCommand = StripCommand(compileCommand);
			if (strippedCommand == null)
				return false;
			var commandLine = strippedCommand + " -E -Wp,-v";

			var stderr = new List<string>();

			int exitCode = Utility.ExecuteCommand(commandLine,
				(line) => ProcessOutputLine(line),
				(line) => stderr.Add(line));

			if (exitCode != 0)
			{
				foreach (var s in stderr)
					Log.Info("stderr: " + s);
				throw new Exception("Command failed with exit code " + exitCode + ": " + commandLine);
			}

			m_currentLine = null;

			if (m_includeStack.Count != 1)
				throw new Exception("Missing return from include: EOF.");
			Debug.Assert(m_includeStack[0] == m_mainFile);

			LeaveFile(true);

			return true;
		}

		private string StripCommand(string command)
		{
			var parts = CommandLine.Split(command);

			if (!parts[0].EndsWith("/gcc") && !parts[0].EndsWith("/g++"))
				return null;

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

		private void ProcessOutputLine(string line)
		{
			if (line.StartsWith('#'))
			{
				//Console.WriteLine(line);

				if (line.Length < 2)
					return;
				if (line[1] != ' ')
					return;
				var pathStart = line.IndexOf('"');
				if (pathStart < 0)
				{
					Log.Warning("Cannot parse line info line: " + line);
					return;
				}

				var pathEnd = line.IndexOf('"', pathStart + 1);
				if (pathEnd < 0)
				{
					Log.Warning("Cannot parse line info line: " + line);
					return;
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
				var internedPath = Intern(path);
				fileEntry = new FileEntry(internedPath, isMainFile);
				m_files.Add(internedPath, fileEntry);
			}
			return fileEntry;
		}

		private string Intern(string str)
		{
			return m_stringMap.Intern(str);
		}

		private void ProcessContentLine(string line)
		{
			m_currentContents.Add(line);
		}

		private FileEntry CurrentFile()
		{
			return m_includeStack[m_includeStack.Count - 1];
		}

		private readonly SynchronizedStringMap m_stringMap;

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
		public static void ProcessCompileCommandsJson(string compileCommandsJson, string outputPath)
		{
			var pa = new ParallelAnalyzer();
			pa.Run(compileCommandsJson, outputPath);
		}

		private ParallelAnalyzer()
		{
			m_dataModel_tus = new List<DMTranslationUnit>();
		}

		private const int OUTPUT_WRITER_FLUSH_THRESHOLD = 100 * 1024 * 1024;
		private const int THREAD_COUNT = 24;

		private void Run(string compileCommandsJson, string outputPath)
		{
			var json = File.ReadAllText(compileCommandsJson);
			var compileCommands = JsonSerializer.Deserialize<CompileCommandsJson>("{ \"entries\": " + json + " }");

			foreach (var entry in compileCommands.entries)
			{
				m_jobList.AddLast(entry);
				//if (m_jobList.Count >= 100)
				//	break;
			}
			m_totalJobCount = m_jobList.Count;

			var threads = new List<Thread>();
			for (int i = 0; i < THREAD_COUNT; i++)
				threads.Add(new Thread(() => ThreadFunction()));
			foreach (var t in threads)
				t.Start();
			foreach (var t in threads)
				t.Join();

			var dataModel_paths = m_pathMap.GetStrings();

			using (var outputStream = File.Create(outputPath))
			using (var outputWriter = new Utf8JsonWriter(outputStream, new JsonWriterOptions{ Indented = true, SkipValidation = false }))
			{
				var serializerOptions = new JsonSerializerOptions();

				outputWriter.WriteStartObject();

				outputWriter.WriteStartArray("paths");
				foreach (var p in dataModel_paths)
					outputWriter.WriteStringValue(p);
				outputWriter.WriteEndArray();
				outputWriter.Flush();

				outputWriter.WriteStartArray("tus");
				foreach (var tu in m_dataModel_tus)
				{ 
					JsonSerializer.Serialize(outputWriter, tu, serializerOptions);
					if (outputWriter.BytesPending > OUTPUT_WRITER_FLUSH_THRESHOLD)
						outputWriter.Flush();
				}
				outputWriter.WriteEndArray();

				outputWriter.WriteEndObject();
				outputWriter.Flush();

				outputStream.Flush();
			}
		}

		private void ThreadFunction()
		{
			var scratch = new SerializerScratch();
			while (true)
			{
				CompileCommandsJsonEntry file;

				lock (m_jobListMutex)
				{
					if (m_jobList.Count == 0)
						break;
					file = m_jobList.First.Value;
					m_jobList.RemoveFirst();
				}

				if (!File.Exists(file.file))
				{
					Log.Info("Skipping non-existant file '" + file.file + "'.");
					continue;
				}

				var tuResult = Analyzer.AnalyzeCompileCommand(file.command, m_stringMap);
				if (tuResult == null)
				{
					Log.Info("Skipping unsupported compile command for file '" + file.file + "'.");
					continue;
				}

				lock (m_jobListMutex)
				{
					m_processedJobCount++;
					Console.WriteLine("{0}/{1} '{2}'...", m_processedJobCount, m_totalJobCount, file.file);
				}

				var dmTu = ToDataModel(tuResult, scratch);
				lock (m_dataModelLock)
					m_dataModel_tus.Add(dmTu);
			}
		}

		class SerializerScratch
		{
			public readonly List<int> inc = new List<int>();
			public readonly List<DMFile> files = new List<DMFile>();
		}

		private DMTranslationUnit ToDataModel(FileEntry translationUnit, SerializerScratch scratch)
		{
			Dictionary<FileEntry, bool> seen = new Dictionary<FileEntry, bool>();
			LinkedList<FileEntry> toProcess = new LinkedList<FileEntry>();

			DMTranslationUnit dmTu = new DMTranslationUnit();
			dmTu.path = MapPath(translationUnit.Path);

			toProcess.AddLast(translationUnit);
			seen[translationUnit] = true;

			scratch.files.Clear();
			while (toProcess.Count > 0)
			{
				var entry = toProcess.First.Value;
				toProcess.RemoveFirst();

				var dmFile = new DMFile();
				dmFile.path = MapPath(entry.Path);
				dmFile.sln = entry.SelfLineCount;
				dmFile.stk = entry.SelfTokenCount;

				scratch.inc.Clear();
				foreach (var includeNode in entry.Includes)
				{
					var include = includeNode.Key;
					if (!seen.ContainsKey(include))
					{
						toProcess.AddLast(include);
						seen[include] = true;
					}

					scratch.inc.Add(MapPath(include.Path));
				}

				dmFile.inc = scratch.inc.ToArray();
				scratch.inc.Clear();

				scratch.files.Add(dmFile);
			}

			dmTu.files = scratch.files.ToArray();
			scratch.files.Clear();

			return dmTu;
		}

		private int MapPath(string path)
		{
			return m_pathMap.Add(path);
		}

		private readonly object m_jobListMutex = new object();
		private readonly LinkedList<CompileCommandsJsonEntry> m_jobList = new LinkedList<CompileCommandsJsonEntry>();
		private int m_totalJobCount;
		private int m_processedJobCount;

		private readonly SynchronizedStringMap m_stringMap = new SynchronizedStringMap();
		private readonly SynchronizedStringMap m_pathMap = new SynchronizedStringMap();

		private readonly object m_dataModelLock = new object();
		private readonly List<DMTranslationUnit> m_dataModel_tus = new List<DMTranslationUnit>();
	}
}

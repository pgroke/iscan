using iscan.dm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace iscan.analyze
{
	ref struct DMReader
	{
		public static DMProject ReadProject(Stream stream)
		{
			var instance = new DMReader(stream);
			var project = instance.ParseProject();
			if (instance.m_tokenType != JsonTokenType.None)
				throw new Exception("Syntax error: found tokens after end of project.");

			return project;
		}

		DMReader(Stream stream)
		{
			m_intArrayScratch = new List<int>();
			m_fileArrayScratch = new List<DMFile>();
			m_stringArrayScratch = new List<string>();

			m_stream = stream;
			m_buffer = new byte[8 * 1024 * 1024];
			m_bufferThreshold = 512 * 1024;
			m_bufferFill = 0;

			m_streamLength = m_stream.Length;
			m_totalRead = 0;
			m_isLastBuffer = m_streamLength == 0;

			if (m_isLastBuffer)
				m_reader = new Utf8JsonReader(new ReadOnlySpan<byte>(m_buffer, 0, 0), m_isLastBuffer, new JsonReaderState());
			else
				m_reader = default;

			m_tokenType = JsonTokenType.None;

			MoveNext();
		}

		DMProject ParseProject()
		{
			Parse(JsonTokenType.StartObject);

			var project = new DMProject {
				paths = null,
				tus = null
			};

			while (true)
			{
				if (m_tokenType == JsonTokenType.EndObject)
				{
					Parse(JsonTokenType.EndObject);
					break;
				}

				if (m_tokenType == JsonTokenType.PropertyName)
					ParseProjectProperty(project);
				else
					throw new Exception("Syntax error: expected " + JsonTokenType.PropertyName + " or " + JsonTokenType.EndObject + ", found " + m_tokenType);
			}

			if (project.paths == null)
				throw new Exception("Project: missing paths property.");
			if (project.tus == null)
				throw new Exception("Project: missing tus property.");

			return project;
		}

		void ParseProjectProperty(DMProject project)
		{
			var prop = ParseAsString(JsonTokenType.PropertyName);
			switch (prop)
			{
				case "paths":
					if (project.paths != null)
						throw new Exception("Syntax error: project duplicate entry 'paths'.");
					project.paths = ParseStringArray();
					break;

				case "tus":
					if (project.tus != null)
						throw new Exception("Syntax error: project duplicate entry 'tus'.");
					project.tus = ParseTranslationUnitArray();
					break;
				default:
					throw new Exception("Syntax error: project unknoen entry '" + prop + "'.");
			}
		}

		DMTranslationUnit[] ParseTranslationUnitArray()
		{
			Parse(JsonTokenType.StartArray);
			var list = new List<DMTranslationUnit>();
			while (m_tokenType == JsonTokenType.StartObject)
				list.Add(ParseTranslationUnit());
			Parse(JsonTokenType.EndArray);
			return list.ToArray();
		}

		DMTranslationUnit ParseTranslationUnit()
		{
			Parse(JsonTokenType.StartObject);

			var tu = new DMTranslationUnit {
				path = INT_SENTINEL,
				files = null
			};

			while (true)
			{
				if (m_tokenType == JsonTokenType.EndObject)
				{
					Parse(JsonTokenType.EndObject);
					break;
				}

				if (m_tokenType == JsonTokenType.PropertyName)
					ParseTranslationUnitProperty(tu);
				else
					throw new Exception("Syntax error: expected " + JsonTokenType.PropertyName + " or " + JsonTokenType.EndObject + ", found " + m_tokenType);
			}

			if (tu.path == INT_SENTINEL)
				throw new Exception("Translation unit: missing path property.");
			if (tu.files == null)
				throw new Exception("Translation unit: missing files property.");

			return tu;
		}

		void ParseTranslationUnitProperty(DMTranslationUnit tu)
		{
			var prop = ParseAsString(JsonTokenType.PropertyName);
			switch (prop)
			{
				case "path":
					if (tu.path != INT_SENTINEL)
						throw new Exception("Syntax error: project duplicate entry 'path'.");
					tu.path = ParseInt32();
					break;

				case "files":
					if (tu.files != null)
						throw new Exception("Syntax error: project duplicate entry 'files'.");
					tu.files = ParseFileArray();
					break;
				default:
					throw new Exception("Syntax error: project unknoen entry '" + prop + "'.");
			}
		}

		DMFile[] ParseFileArray()
		{
			m_fileArrayScratch.Clear();

			Parse(JsonTokenType.StartArray);
			while (m_tokenType == JsonTokenType.StartObject)
				m_fileArrayScratch.Add(ParseFile());
			Parse(JsonTokenType.EndArray);

			var result = m_fileArrayScratch.ToArray();
			m_fileArrayScratch.Clear();
			return result;
		}

		DMFile ParseFile()
		{
			Parse(JsonTokenType.StartObject);

			var file = new DMFile {
				path = INT_SENTINEL,
				stk = INT_SENTINEL,
				sln = INT_SENTINEL,
				inc = null
			};

			while (true)
			{
				if (m_tokenType == JsonTokenType.EndObject)
				{
					Parse(JsonTokenType.EndObject);
					break;
				}

				if (m_tokenType == JsonTokenType.PropertyName)
					ParseFileProperty(file);
				else
					throw new Exception("Syntax error: expected " + JsonTokenType.PropertyName + " or " + JsonTokenType.EndObject + ", found " + m_tokenType);
			}

			if (file.path == INT_SENTINEL)
				throw new Exception("File: missing path property.");
			if (file.stk == INT_SENTINEL)
				throw new Exception("File: missing stk property.");
			if (file.sln == INT_SENTINEL)
				throw new Exception("File: missing sln property.");
			if (file.inc == null)
				throw new Exception("File: missing inc property.");

			return file;
		}

		void ParseFileProperty(DMFile file)
		{
			if (file.path == INT_SENTINEL && m_reader.ValueTextEquals(s_text_path.EncodedUtf8Bytes))
			{
				MoveNext();
				file.path = ParseInt32();
			}
			else if (file.stk == INT_SENTINEL && m_reader.ValueTextEquals(s_text_stk.EncodedUtf8Bytes))
			{
				MoveNext();
				file.stk = ParseInt32();
			}
			else if (file.sln == INT_SENTINEL && m_reader.ValueTextEquals(s_text_sln.EncodedUtf8Bytes))
			{
				MoveNext();
				file.sln = ParseInt32();
			}
			else if (file.inc == null && m_reader.ValueTextEquals(s_text_inc.EncodedUtf8Bytes))
			{
				MoveNext();
				file.inc = ParseInt32Array();
			}
			else
			{
				if (m_reader.ValueTextEquals(s_text_path.EncodedUtf8Bytes))
					throw new Exception("Syntax error: project duplicate entry 'path'.");
				if (m_reader.ValueTextEquals(s_text_stk.EncodedUtf8Bytes))
					throw new Exception("Syntax error: project duplicate entry 'stk'.");
				if (m_reader.ValueTextEquals(s_text_sln.EncodedUtf8Bytes))
					throw new Exception("Syntax error: project duplicate entry 'sln'.");
				if (m_reader.ValueTextEquals(s_text_inc.EncodedUtf8Bytes))
					throw new Exception("Syntax error: project duplicate entry 'inc'.");

				throw new Exception("Syntax error: project unknoen entry '" + m_reader.GetString() + "'.");
			}
		}

		string[] ParseStringArray()
		{
			m_stringArrayScratch.Clear();

			Parse(JsonTokenType.StartArray);
			while (m_tokenType == JsonTokenType.String)
				m_stringArrayScratch.Add(ParseAsString(JsonTokenType.String));
			Parse(JsonTokenType.EndArray);

			var result = m_stringArrayScratch.ToArray();
			m_stringArrayScratch.Clear();
			return result;
		}

		int[] ParseInt32Array()
		{
			m_intArrayScratch.Clear();

			Parse(JsonTokenType.StartArray);
			while (m_tokenType == JsonTokenType.Number)
				m_intArrayScratch.Add(ParseInt32());
			Parse(JsonTokenType.EndArray);

			var result = m_intArrayScratch.ToArray();
			m_intArrayScratch.Clear();
			return result;
		}

		int ParseInt32()
		{
			CheckTokenType(JsonTokenType.Number);
			int n = m_reader.GetInt32();
			MoveNext();
			return n;
		}

		string ParseAsString(JsonTokenType tokenType)
		{
			CheckTokenType(tokenType);
			var str = m_reader.GetString();
			if (str == null)
				throw new Exception("Syntax error: could not parse string.");
			MoveNext();
			return str;
		}

		void Parse(JsonTokenType tokenType)
		{
			CheckTokenType(tokenType);
			MoveNext();
		}

		void CheckTokenType(JsonTokenType tokenType)
		{
			if (m_tokenType != tokenType)
				throw new Exception("Syntax error: expected " + tokenType + ", found " + m_tokenType + ".");
		}

		void MoveNext()
		{
			if (TryMoveNextNoFillBuffer())
				return;

			if (m_isLastBuffer)
				return;

			FillBuffer();
			TryMoveNextNoFillBuffer();
		}

		bool TryMoveNextNoFillBuffer()
		{
			if (m_reader.Read())
			{
				m_tokenType = m_reader.TokenType;
				Debug.Assert(m_tokenType != JsonTokenType.None);
				return true;
			}
			else
			{
				m_tokenType = JsonTokenType.None;
				return false;
			}
		}

		void FillBuffer()
		{
			Debug.Assert(m_bufferFill <= m_buffer.Length);

			long consumed = m_reader.BytesConsumed;
			Debug.Assert(consumed >= 0);
			if (consumed > m_bufferFill)
				throw new Exception("Internal error: consumed > m_bufferFill");

			int unconsumed = (int)(m_bufferFill - consumed);
			if (unconsumed >= m_bufferThreshold)
				return;

			if (m_isLastBuffer)
				return;

			if (consumed > 0)
			{
				Array.Copy(m_buffer, (int)consumed, m_buffer, 0, unconsumed);
				m_bufferFill = unconsumed;
			}

			int readable = m_buffer.Length - m_bufferFill;
			int read = m_stream.Read(m_buffer, m_bufferFill, readable);
			Debug.Assert(read <= readable);
			if (read == 0)
				throw new Exception("Read error: Unexpected end of file.");

			m_bufferFill += read;
			m_totalRead += read;
			if (m_totalRead > m_streamLength)
				throw new Exception("Read error: read more than Stream.Length.");

			m_isLastBuffer = m_totalRead == m_streamLength;

			m_reader = new Utf8JsonReader(new ReadOnlySpan<byte>(m_buffer, 0, m_bufferFill), m_isLastBuffer, m_reader.CurrentState);
		}

		const int INT_SENTINEL = int.MinValue;

		static readonly JsonEncodedText s_text_path = JsonEncodedText.Encode("path");
		static readonly JsonEncodedText s_text_sln = JsonEncodedText.Encode("sln");
		static readonly JsonEncodedText s_text_stk = JsonEncodedText.Encode("stk");
		static readonly JsonEncodedText s_text_inc = JsonEncodedText.Encode("inc");

		readonly List<int> m_intArrayScratch;
		readonly List<DMFile> m_fileArrayScratch;
		readonly List<string> m_stringArrayScratch;

		readonly Stream m_stream;
		readonly long m_streamLength;
		readonly byte[] m_buffer;
		readonly int m_bufferThreshold;

		int m_bufferFill;
		long m_totalRead;
		bool m_isLastBuffer;

		Utf8JsonReader m_reader;
		JsonTokenType m_tokenType;
	}
}

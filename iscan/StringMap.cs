﻿using System;
using System.Collections.Generic;
using System.Text;

namespace iscan
{
	class StringMap
	{
		public List<string> GetStrings()
		{
			return new List<string>(m_strings);
		}

		public string Map(int id)
		{
			return m_strings[id];
		}

		public int Map(string str)
		{
			return m_index[str];
		}

		public int Add(string str)
		{
			if (m_index.ContainsKey(str))
				return m_index[str];

			m_strings.Add(str);
			int id = m_strings.Count - 1;
			m_index[str] = id;
			return id;
		}

		public string Intern(string str)
		{
			return Map(Add(str));
		}

		private readonly List<string> m_strings = new List<string>();
		private readonly Dictionary<string, int> m_index = new Dictionary<string, int>();
	}

	class SynchronizedStringMap
	{
		public List<string> GetStrings()
		{
			lock (m_stringMapLock)
				return m_stringMap.GetStrings();
		}

		public string Map(int id)
		{
			lock (m_stringMapLock)
				return m_stringMap.Map(id);
		}

		public int Map(string str)
		{
			lock (m_stringMapLock)
				return m_stringMap.Map(str);
		}

		public int Add(string str)
		{
			lock (m_stringMapLock)
				return m_stringMap.Add(str);
		}

		public string Intern(string str)
		{
			lock (m_stringMapLock)
				return m_stringMap.Intern(str);
		}

		private readonly object m_stringMapLock = new object();
		private readonly StringMap m_stringMap = new StringMap();
	}
}
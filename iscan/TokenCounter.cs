using System;
using System.Collections.Generic;
using System.Text;

namespace iscan
{
	static class TokenCounter
	{
		public static int CountTokens(List<string> lines)
		{
			int tokenCount = 0;
			foreach (var line in lines)
			{
				var lastChar = ' ';
				var lastType = CharType.Whitespace;
				foreach (var ch in line)
				{
					if ((lastChar == ':' && ch == ':')
						|| (lastChar == '>' && ch == '>')
						|| (lastChar == '<' && ch == '<')
						|| (lastChar == '&' && ch == '&')
						|| (lastChar == '|' && ch == '|')
						|| (lastChar == '+' && ch == '+')
						|| (lastChar == '-' && ch == '-')
						|| (lastChar == '=' && ch == '=')
						|| (lastChar == '!' && ch == '=')
						|| (lastChar == '+' && ch == '=')
						|| (lastChar == '-' && ch == '=')
						|| (lastChar == '*' && ch == '=')
						|| (lastChar == '/' && ch == '=')
						|| (lastChar == '%' && ch == '=')
						|| (lastChar == '&' && ch == '=')
						|| (lastChar == '|' && ch == '=')
						|| (lastChar == '-' && ch == '>')
						)
					{
						lastChar = ' ';
						continue;
					}

					var type = Classify(ch);
					if (type != lastType && type != CharType.Whitespace)
						tokenCount++;

					lastType = type;
				}
			}

			return tokenCount;
		}

		enum CharType
		{
			Whitespace,
			SymbolChar,
			Other,
		}

		static TokenCounter()
		{
			TagCharRange(CharType.Other, (char)0, (char)255);
			TagCharRange(CharType.SymbolChar, 'a', 'z');
			TagCharRange(CharType.SymbolChar, 'A', 'Z');
			TagCharRange(CharType.SymbolChar, '0', '9');
			TagChars(CharType.SymbolChar, '_');
			TagChars(CharType.Whitespace, ' ', '\t', '\n', '\v', '\f', '\r');
		}

		private static CharType Classify(char ch)
		{
			int chi = (int)ch;
			if (chi < 0 || chi > 255)
				return CharType.SymbolChar; // Doesn't really matter

			return s_charMap[chi];
		}

		private static void TagCharRange(CharType type, char begin, char end)
		{
			for (char ch = begin; ch <= end; ch++)
				s_charMap[ch] = type;
		}

		private static void TagChars(CharType type, params char[] chs)
		{
			foreach (char ch in chs)
				s_charMap[ch] = type;
		}

		private static CharType[] s_charMap = new CharType[256];
	}
}

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.CommandLine.Suggestions;
using System.IO;
using System.Linq;

namespace CloneLeeroy
{
	internal sealed class ProjectSuggestionSource : ISuggestionSource
	{
		public ProjectSuggestionSource(string configurationPath) => m_configurationPath = configurationPath;

		public IEnumerable<string?> GetSuggestions(ParseResult? parseResult = null, string? textToMatch = null)
		{
			m_files ??= Directory.GetFiles(m_configurationPath, "*.json").Select(x => Path.GetFileNameWithoutExtension(x)).ToArray();
			return m_files.Where(x => string.IsNullOrEmpty(textToMatch) || x.IndexOf(textToMatch, StringComparison.OrdinalIgnoreCase) != -1);
		}

		private readonly string m_configurationPath;
		private string[]? m_files;
	}
}

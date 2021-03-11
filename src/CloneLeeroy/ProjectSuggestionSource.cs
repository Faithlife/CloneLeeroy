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
			// HACK: if no parseResult is supplied, assume this is being invoked from the HelpBuilder (i.e., --help) and avoid spamming the output with all the project names
			if (parseResult is null)
				return Enumerable.Empty<string?>();

			m_files ??= Directory.GetFiles(m_configurationPath, "*.json").Select(x => Path.GetFileNameWithoutExtension(x)).ToArray();
			return m_files.Where(x => string.IsNullOrEmpty(textToMatch) || x.IndexOf(textToMatch, StringComparison.OrdinalIgnoreCase) != -1);
		}

		private readonly string m_configurationPath;
		private string[]? m_files;
	}
}

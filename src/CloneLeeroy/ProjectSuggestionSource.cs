using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.CommandLine.Suggestions;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CloneLeeroy
{
	internal sealed class ProjectSuggestionSource : ISuggestionSource
	{
		public ProjectSuggestionSource(Task<bool> readConfigurationTask, string configurationPath)
		{
			m_readConfigurationTask = readConfigurationTask;
			m_configurationPath = configurationPath;
		}

		public IEnumerable<string?> GetSuggestions(ParseResult? parseResult = null, string? textToMatch = null)
		{
			if (!Directory.Exists(m_configurationPath))
			{
				try
				{
					m_readConfigurationTask.GetAwaiter().GetResult();
				}
				catch (Exception)
				{
					// ignore any failure to clone the configuration repository
				}
			}

			if (m_files is null)
			{
				try
				{
					m_files = Directory.GetFiles(m_configurationPath, "*.json").Select(x => Path.GetFileNameWithoutExtension(x)).ToArray();
				}
				catch (Exception)
				{
					// ignore any failure to read the directory; provide no suggestions
					return Array.Empty<string?>();
				}
			}

			return m_files.Where(x => string.IsNullOrEmpty(textToMatch) || x.IndexOf(textToMatch, StringComparison.OrdinalIgnoreCase) != -1);
		}

		private readonly Task<bool> m_readConfigurationTask;
		private readonly string m_configurationPath;
		private string[]? m_files;
	}
}

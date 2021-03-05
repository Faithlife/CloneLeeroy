using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CloneLeeroy
{
	internal sealed class LeeroyConfiguration
	{
		[JsonPropertyName("buildUrls")]
		public string[]? BuildUrls { get; set; }

		[JsonPropertyName("repoUrl")]
		public string? RepositoryUrl { get; set; }

		[JsonPropertyName("branch")]
		public string? BranchName { get; set; }

		[JsonPropertyName("submodules")]
		public Dictionary<string, string>? Submodules { get; set; }
	}
}

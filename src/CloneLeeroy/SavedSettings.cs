using System.Text.Json.Serialization;

namespace CloneLeeroy
{
	internal sealed class SavedSettings
	{
		[JsonPropertyName("leeroyConfig")]
		public string? ProjectName { get; set; }
	}
}

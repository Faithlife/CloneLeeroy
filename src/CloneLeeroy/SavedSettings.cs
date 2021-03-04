using System.Text.Json.Serialization;

namespace CloneLeeroy
{
	public sealed class SavedSettings
	{
		[JsonPropertyName("leeroyConfig")]
		public string? ProjectName { get; set; }
	}
}

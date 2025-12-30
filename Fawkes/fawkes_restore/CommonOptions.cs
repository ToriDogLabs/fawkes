using System.Text.Json.Serialization;

namespace fawkes_restore;

public class CommonOptions
{
	public string AccessKey { get; set; } = string.Empty;

	public string? Endpoint { get; set; }

	public string Path { get; set; } = string.Empty;

	public string SecretKey { get; set; } = string.Empty;
}

[JsonSerializable(typeof(CommonOptions))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
using System.Text.Json.Serialization;

namespace Fawkes.Api;

public record S3BackupLocation
{
	[JsonPropertyName("id")]
	public required string Id { get; set; }
	[JsonPropertyName("accessKey")]
	public required string AccessKey { get; init; }
	[JsonPropertyName("secretKey")]
	public required string SecretKey { get; init; }
	[JsonPropertyName("bucket")]
	public required string Bucket { get; init; }
	[JsonPropertyName("endpoint")]
	public required string Endpoint { get; init; }
	[JsonPropertyName("forcePathStyle")]
	public bool? ForcePathStyle { get; init; } = true;
	[JsonPropertyName("prefix")]
	public string? Prefix { get; init; }
	[JsonPropertyName("friendlyName")]
	public required string FriendlyName { get; init; }

	public string S3Path
	{
		get
		{
			var path = $"s3://{Bucket}";
			if (!string.IsNullOrWhiteSpace(Prefix))
			{
				path += $"/{Prefix}";
			}
			return path;
		}
	}
}
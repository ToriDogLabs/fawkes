using Amazon.S3;
using ICSharpCode.SharpZipLib.BZip2;
using System.Text.Json;

namespace fawkes_restore;

public class RestoreWalService
{
	public enum ReturnCodes
	{
		Success,
		UnknownPgData,
		MissingRecoverySignal,
		InvalidRecoverySignalConfig,
		DownloadError,
	}

	public static async Task<ReturnCodes> Run(Options options)
	{
		var pgData = Environment.GetEnvironmentVariable("PGDATA");
		if (pgData == null)
		{
			Console.WriteLine("Unable to determine pgdirectory");
			return ReturnCodes.UnknownPgData;
		}
		var signalPath = Path.Join(pgData, "recovery.signal");
		if (!File.Exists(signalPath))
		{
			Console.WriteLine("Missing recovery file");
			return ReturnCodes.MissingRecoverySignal;
		}
		var config = await JsonSerializer.DeserializeAsync(
			new FileStream(signalPath, FileMode.Open), SourceGenerationContext.Default.CommonOptions);

		if (config == null)
		{
			Console.WriteLine("Unable to get cloud information from recovery.signal");
			return ReturnCodes.InvalidRecoverySignalConfig;
		}

		using var client = new AmazonS3Client(
			new Amazon.Runtime.BasicAWSCredentials(config.AccessKey, config.SecretKey),
			new AmazonS3Config
			{
				ServiceURL = config.Endpoint,
				ForcePathStyle = true
			}
		);
		var (bucket, prefix) = RestoreService.ParsePath(config.Path);
		var dir = options.File[..16];

		try
		{
			Console.WriteLine($"Downloading wal from {bucket} from {prefix}/wals/{dir}/{options.File}.bz2");
			using var response = await client.GetObjectAsync(new()
			{
				BucketName = bucket,
				Key = $"{prefix}/wals/{dir}/{options.File}.bz2"
			});

			BZip2.Decompress(response.ResponseStream, new FileStream(options.Path, FileMode.CreateNew), true);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failure downloading wal file {ex.Message}");
			return ReturnCodes.DownloadError;
		}
		return ReturnCodes.Success;
	}

	public class Options
	{
		public string File { get; set; } = string.Empty;

		public string Path { get; set; } = string.Empty;
	}
}
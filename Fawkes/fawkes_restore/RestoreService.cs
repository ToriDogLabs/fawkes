using Amazon.S3;
using Amazon.S3.Transfer;
using ICSharpCode.SharpZipLib.BZip2;
using System.Formats.Tar;
using System.Text.Json;

namespace fawkes_restore;

public static class RestoreService
{
	public static (string Bucket, string PathPrefix) ParsePath(string path)
	{
		var parts = path.Split('/');
		var bucket = parts.First();
		var prefix = $"{string.Join("/", parts.Skip(1))}";
		return (bucket, prefix);
	}

	public static async Task<int> Run(Options options)
	{
		options.RecoverDir ??= "/pgdata";
		using var client = new AmazonS3Client(
			new Amazon.Runtime.BasicAWSCredentials(options.AccessKey, options.SecretKey),
			new AmazonS3Config
			{
				ServiceURL = options.Endpoint,
				ForcePathStyle = true
			}
		);
		var (bucket, prefix) = ParsePath(options.Path);
		var key = $"{prefix}/base/{options.BackupId}/data.tar.bz2";
		var bzipPath = Path.Join(options.RecoverDir, "data.tar.bz2");

		var transferUtility = new TransferUtility(client);
		await transferUtility.DownloadAsync(bzipPath, bucket, key);

		var tarPath = Path.Join(options.RecoverDir, "data.tar");

		using (var bzipStream = new FileStream(bzipPath, FileMode.Open))
		{
			BZip2.Decompress(bzipStream, new FileStream(tarPath, FileMode.Create), true);
		}

		using (var tarStream = new FileStream(tarPath, FileMode.Open))
		{
			await TarFile.ExtractToDirectoryAsync(tarStream, options.RecoverDir, false);
		}

		var confPath = Path.Join(options.RecoverDir, "postgresql.conf");
		var lines = (await File.ReadAllLinesAsync(confPath)).ToList();
		for (var i = 0; i < lines.Count; ++i)
		{
			if (lines[i].StartsWith("restore_command") || lines[i].StartsWith("recovery_target_time"))
			{
				lines[i] = $"#{lines[i]}";
			}
		}
		lines.Add("restore_command = 'fawkes_restore restore_wal -f %f -p %p'");
		if (!string.IsNullOrWhiteSpace(options.TargetTime))
		{
			lines.Add("recovery_target_time = '${target_time}'");
		}

		await File.WriteAllLinesAsync(confPath, lines);

		var signalPath = Path.Join(options.RecoverDir, "recovery.signal");
		File.WriteAllText(signalPath, JsonSerializer.Serialize(options, SourceGenerationContext.Default.CommonOptions));
		File.Delete(tarPath);
		File.Delete(bzipPath);
		return 0;
	}

	public class Options : CommonOptions
	{
		public string BackupId { get; set; } = string.Empty;

		public string? RecoverDir { get; set; }

		public string? TargetTime { get; set; }
	}
}
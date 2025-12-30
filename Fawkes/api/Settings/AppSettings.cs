using NodaTime;
using Quartz;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tools;

namespace Fawkes.Api.Settings;

public class AppSettings
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
	private ISchedulerFactory schedulerFactory;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

	[JsonPropertyName("databases")]
	public Dictionary<string, DatabaseSettings> Databases { get; init; } = [];

	[JsonPropertyName("s3Locations")]
	public Dictionary<string, S3BackupLocation> S3Locations { get; set; } = [];

	[JsonPropertyName("timezone")]
	public string? TimeZoneName { get; set; }

	public DateTimeZone TimeZone => TimeZoneName == null ? DateTimeZone.Utc : DateTimeZoneProviders.Tzdb[TimeZoneName];

	public static AppSettings Load(ISchedulerFactory schedulerFactory)
	{
		try
		{
			if (File.Exists(Constants.SettingsPath))
			{
				var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Constants.SettingsPath)) ?? new();
				settings.schedulerFactory = schedulerFactory;
				return settings;
			}
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex);
		}
		var settings2 = new AppSettings
		{
			schedulerFactory = schedulerFactory
		};
		return settings2;
	}

	public async Task Save()
	{
		await WriteBarmanConfig();
		await WriteAwsFiles();
		var scheduler = await schedulerFactory.GetScheduler();
		await scheduler.ScheduleJob(JobBuilder.Create<SaveAppSettingsJob>().Build(),
			TriggerBuilder.Create().StartNow().Build());
	}

	private async Task WriteAwsFiles()
	{
		var configDoc = new IniDocument();
		var credentialsDoc = new IniDocument();
		foreach (var kvp in S3Locations)
		{
			var config = kvp.Value;
			var section = configDoc.AddSection($"profile {kvp.Key}");
			section["endpoint_url"] = config.Endpoint;
			section["addressing_style"] = (config.ForcePathStyle ?? false) ? "path" : "auto";
			section["bucket_name"] = config.Bucket;
			section["path_prefix"] = config.Prefix ?? string.Empty;

			var credentialsSection = credentialsDoc.AddSection(kvp.Key);
			credentialsSection["aws_access_key_id"] = config.AccessKey;
			credentialsSection["aws_secret_access_key"] = config.SecretKey;
		}

		await configDoc.WriteToFile(Constants.AwsConfigPath);
		await credentialsDoc.WriteToFile(Constants.AwsCredentialsPath);
	}

	private async Task WriteBarmanConfig()
	{
		var document = new IniDocument();
		foreach (var dbKvp in Databases)
		{
			var dbId = dbKvp.Key;
			var db = dbKvp.Value;
			var section = document.AddSection(dbId);
			var port = db.Port ?? "5432";
			section.Add("conninfo", $"host={db.Host} port={port} user={db.ReplicationUser} password={db.ReplicationPassword} dbname={db.Database} sslmode={(db.UseSsl ? "enable" : "disable")}");
			section.Add("streaming_conninfo", $"host={db.Host} port={port} user={db.ReplicationUser} password={db.ReplicationPassword} dbname={db.Database} sslmode={(db.UseSsl ? "enable" : "disable")}");
			section.Add("backup_method", "postgres");
			section.Add("streaming_archiver", "on");
			section.Add("slot_name", Constants.GetSlotName(dbId));
			section.Add("create_slot", "auto");
			section.Add("pre_archive_retry_script", $"'{Constants.GetDbWalScriptPath(dbId)} ${{BARMAN_FILE}}'");
			section.Add("post_backup_retry_script", $"'{Constants.GetDbBackupScriptPath(dbId)}'");
			section.Add("retention_policy", "REDUNDANCY 1");
			var backupLocations = db.BackupLocations.Select(id =>
			{
				if (S3Locations.TryGetValue(id, out var location))
				{
					return location;
				}
				return null;
			}).WhereNotNull();
			await WriteWalScript(dbId, backupLocations);
			await WriteBackupScript(dbId, backupLocations);
		}
		await document.WriteToFile(Constants.DbConfigPath);
	}

	private async Task WriteWalScript(string dbId, IEnumerable<S3BackupLocation> s3Configs)
	{
		var builder = new StringBuilder();
		builder.AppendLine("#!/bin/bash");
		builder.AppendLine("set -euo pipefail");
		builder.AppendLine();
		builder.AppendLine("WAL_PATH=\"$1\"");
		builder.AppendLine();
		var index = 1;
		foreach (var config in s3Configs)
		{
			builder.AppendLine($"barman-cloud-wal-archive --endpoint-url {config.Endpoint} -P {config.Id} -j {config.S3Path} {dbId} \"$WAL_PATH\" &");
			builder.AppendLine($"pid{index}=$!");
			builder.AppendLine();
			index++;
		}
		builder.AppendLine();
		builder.AppendLine("# Fail if any fails");
		for (var i = 1; i < index; i++)
		{
			builder.AppendLine($"wait $pid{i}");
		}

		var scriptPath = Constants.GetDbWalScriptPath(dbId);
		await WriteFile(scriptPath, builder.ToString());
		SetScriptPermissions(scriptPath);
	}

	private async Task WriteFile(string path, string content)
	{
		var dir = Path.GetDirectoryName(path);
		if (dir != null)
		{
			if (!Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}
			await File.WriteAllTextAsync(path, content);
		}
	}

	private void SetScriptPermissions(string scriptPath)
	{
#pragma warning disable CA1416 // Validate platform compatibility
		File.SetUnixFileMode(scriptPath, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite
			| UnixFileMode.GroupExecute | UnixFileMode.GroupRead | UnixFileMode.GroupWrite
			| UnixFileMode.OtherRead | UnixFileMode.OtherExecute | UnixFileMode.OtherWrite);
#pragma warning restore CA1416 // Validate platform compatibility
	}

	private async Task WriteBackupScript(string dbId, IEnumerable<S3BackupLocation> s3Configs)
	{
		var builder = new StringBuilder();
		builder.AppendLine("#!/bin/bash");
		builder.AppendLine("set -euo pipefail");
		builder.AppendLine();

		var index = 1;
		foreach (var config in s3Configs)
		{
			builder.AppendLine($"barman-cloud-backup --endpoint-url {config.Endpoint} -P {config.Id} -j {config.S3Path} {dbId} &");
			builder.AppendLine($"pid{index}=$!");
			index++;
		}

		builder.AppendLine();
		builder.AppendLine("# Fail if any fails");
		for (var i = 1; i < index; i++)
		{
			builder.AppendLine($"wait $pid{i}");
		}

		var scriptPath = Constants.GetDbBackupScriptPath(dbId);
		await WriteFile(scriptPath, builder.ToString());
		SetScriptPermissions(scriptPath);
	}
}

public class DatabaseSettings
{
	[JsonPropertyName("id")]
	public required string Id { get; set; }
	[JsonPropertyName("archivalPolicies")]
	public List<ArchivalPolicy> ArchivalPolicies { get; set; } = [];

	[JsonPropertyName("backupLocations")]
	public List<string> BackupLocations { get; set; } = [];

	[JsonPropertyName("backupSchedule")]
	public List<DbBackupSchedule> BackupSchedule { get; init; } = [];

	[JsonPropertyName("replicationPassword")]
	public string ReplicationPassword { get; set; } = string.Empty;

	[JsonPropertyName("database")]
	public required string Database { get; set; }

	[JsonPropertyName("host")]
	public required string Host { get; set; }

	[JsonPropertyName("name")]
	public required string Name { get; set; }

	[JsonPropertyName("replicationUser")]
	public required string ReplicationUser { get; set; }

	[JsonPropertyName("port")]
	public string? Port { get; set; }

	[JsonPropertyName("ssl")]
	public bool UseSsl { get; set; }

	[JsonPropertyName("retentionPolicy")]
	public RetentionPolicy RetentionPolicy { get; set; } = new()
	{
		MinimumRedundancy = 2,
		Retention = 2,
		RetentionUnits = RetentionUnits.WEEKS
	};
}

public class DbBackupSchedule
{
	[JsonPropertyName("cron")]
	public required string CronSchedule { get; init; }

	[JsonPropertyName("id")]
	public required string Id { get; init; }

	[JsonPropertyName("type")]
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public required BackupType Type { get; init; }
}

[DisallowConcurrentExecution]
public class SaveAppSettingsJob : IJob
{
	private readonly AppSettings appSettings;

	public SaveAppSettingsJob(AppSettings appSettings)
	{
		this.appSettings = appSettings;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		await File.WriteAllTextAsync(Constants.SettingsPath, JsonSerializer.Serialize(appSettings));
	}
}
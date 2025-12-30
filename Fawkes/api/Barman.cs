using Fawkes.Api.Settings;
using NodaTime.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fawkes.Api;

public class Barman
{
	public static async Task<List<Backup>> GetBackups(AppSettings appSettings, string dbId, ArchiveService archiveService)
	{
		var backups = new List<Backup>();
		if (appSettings.Databases.TryGetValue(dbId, out var db))
		{
			foreach (var s3Id in db.BackupLocations)
			{
				if (appSettings.S3Locations.TryGetValue(s3Id, out var s3Config))
				{
					var cloudResult = await Terminal.Run("list backup", "barman-cloud-backup-list", "--format", "json",
						"--endpoint-url", s3Config.Endpoint ?? "", "-P", s3Id, s3Config.S3Path, dbId);
					if (!string.IsNullOrWhiteSpace(cloudResult.StandardOutput))
					{
						var backupList = JsonSerializer.Deserialize<CloudBackupList>(cloudResult.StandardOutput);

						foreach (var backup in backupList?.Backups ?? [])
						{
							var backupResult = await Terminal.Run("show backup", "barman-cloud-backup-keep",
								"--endpoint-url", s3Config.Endpoint ?? "", "-P", s3Id, "-s", s3Config.S3Path, dbId, backup.BackupId);
							if (!Enum.TryParse<KeepStatus>(backupResult.StandardOutput.Trim().Substring(6), out var status))
							{
								status = KeepStatus.nokeep;
							}
							var timestamp = OffsetDateTimePattern.ExtendedIso.Parse(backup.BeginTimeIso).Value.ToInstant();
							var archival = archiveService.DetermineAction(timestamp, dbId);
							backups.Add(new Backup
							{
								BackupId = backup.BackupId,
								Name = backup.BackupName,
								ArchivalStatus = status,
								LocationId = s3Id,
								Timestamp = timestamp,
								Archived = archival.Action == ArchiveAction.Keep,
								ArchivedUntil = archival.ExpirationDate
							});
						}
					}
				}
			}
		}
		return backups;
	}
}

public class CloudBackupList
{
	[JsonPropertyName("backups_list")]
	public List<CloudBackup> Backups { get; set; } = [];
}

public class CloudBackup
{
	[JsonPropertyName("backup_id")]
	public required string BackupId { get; set; }

	[JsonPropertyName("backup_label")]
	public string? BackupLabel { get; set; }

	[JsonPropertyName("backup_name")]
	public string? BackupName { get; set; }

	[JsonPropertyName("begin_offset")]
	public long BeginOffset { get; set; }

	[JsonPropertyName("begin_time")]
	public string? BeginTime { get; set; }

	[JsonPropertyName("begin_time_iso")]
	public required string BeginTimeIso { get; set; }

	[JsonPropertyName("begin_wal")]
	public string? BeginWal { get; set; }

	[JsonPropertyName("begin_xlog")]
	public string? BeginXlog { get; set; }

	[JsonPropertyName("children_backup_ids")]
	public string[]? ChildrenBackupIds { get; set; }

	[JsonPropertyName("cluster_size")]
	public long ClusterSize { get; set; }

	[JsonPropertyName("compression")]
	public string? Compression { get; set; }

	[JsonPropertyName("config_file")]
	public string? ConfigFile { get; set; }

	[JsonPropertyName("copy_stats")]
	public CloudBackupCopyStats? CopyStats { get; set; }

	[JsonPropertyName("data_checksums")]
	public string? DataChecksums { get; set; }

	[JsonPropertyName("deduplicated_size")]
	public long DeduplicatedSize { get; set; }

	[JsonPropertyName("encryption")]
	public string? Encryption { get; set; }

	[JsonPropertyName("end_offset")]
	public long EndOffset { get; set; }

	[JsonPropertyName("end_time")]
	public string? EndTime { get; set; }

	[JsonPropertyName("end_time_iso")]
	public DateTimeOffset? EndTimeIso { get; set; }

	[JsonPropertyName("end_wal")]
	public string? EndWal { get; set; }

	[JsonPropertyName("end_xlog")]
	public string? EndXlog { get; set; }

	[JsonPropertyName("error")]
	public string? Error { get; set; }

	[JsonPropertyName("hba_file")]
	public string? HbaFile { get; set; }

	[JsonPropertyName("ident_file")]
	public string? IdentFile { get; set; }

	[JsonPropertyName("included_files")]
	public string[]? IncludedFiles { get; set; }

	[JsonPropertyName("mode")]
	public string? Mode { get; set; }

	[JsonPropertyName("parent_backup_id")]
	public string? ParentBackupId { get; set; }

	[JsonPropertyName("pgdata")]
	public string? Pgdata { get; set; }

	[JsonPropertyName("server_name")]
	public string? ServerName { get; set; }

	[JsonPropertyName("size")]
	public long Size { get; set; }

	[JsonPropertyName("status")]
	public string? Status { get; set; }

	[JsonPropertyName("summarize_wal")]
	public string? SummarizeWal { get; set; }

	[JsonPropertyName("systemid")]
	public string? SystemId { get; set; }

	[JsonPropertyName("tablespaces")]
	public string[]? Tablespaces { get; set; }

	[JsonPropertyName("timeline")]
	public int Timeline { get; set; }

	[JsonPropertyName("version")]
	public int Version { get; set; }

	[JsonPropertyName("xlog_segment_size")]
	public int XlogSegmentSize { get; set; }

	public class CloudBackupCopyStats
	{
		[JsonPropertyName("copy_time")]
		public double CopyTime { get; set; }

		[JsonPropertyName("total_time")]
		public double TotalTime { get; set; }
	}
}
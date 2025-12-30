using Fawkes.Api.Settings;
using NodaTime;
using Quartz;

namespace Fawkes.Api;

public partial class BarmanCleanupJob : IJob
{
	private readonly AppSettings appSettings;
	private readonly ArchiveService archiveService;
	private readonly IClock clock;
	private readonly DbService dbService;
	private readonly INotificationService notify;

	public BarmanCleanupJob(AppSettings appSettings, DbService dbService, INotificationService notify,
		IClock clock, ArchiveService archiveService)
	{
		this.appSettings = appSettings;
		this.dbService = dbService;
		this.notify = notify;
		this.clock = clock;
		this.archiveService = archiveService;
	}

	public async Task Execute(IJobExecutionContext? context = null)
	{
		var now = clock.GetCurrentInstant().InZone(appSettings.TimeZone).LocalDateTime;
		foreach (var (dbId, dbSettings) in appSettings.Databases)
		{
			IEnumerable<Backup> backups = await dbService.GetNoKeepBackups(dbId);
			if (dbSettings.RetentionPolicy.MinimumRedundancy > 0)
			{
				backups = backups.Skip(dbSettings.RetentionPolicy.MinimumRedundancy);
			}
			if (dbSettings.RetentionPolicy.Retention > 0)
			{
				var recoveryTimestamp = clock.GetCurrentInstant().InUtc().LocalDateTime;
				switch (dbSettings.RetentionPolicy.RetentionUnits)
				{
					case RetentionUnits.DAYS:
						recoveryTimestamp = recoveryTimestamp.Minus(Period.FromDays(dbSettings.RetentionPolicy.Retention));
						break;

					case RetentionUnits.WEEKS:
						recoveryTimestamp = recoveryTimestamp.Minus(Period.FromWeeks(dbSettings.RetentionPolicy.Retention));
						break;

					case RetentionUnits.MONTHS:
						recoveryTimestamp = recoveryTimestamp.Minus(Period.FromMonths(dbSettings.RetentionPolicy.Retention));
						break;
				}
				backups = backups.SkipWhile(b => b.Timestamp.InUtc().LocalDateTime > recoveryTimestamp);
			}
			backups = backups.Where(b => archiveService.DetermineAction(b.Timestamp, dbId).Action == ArchiveAction.None);

			foreach (var backup in backups)
			{
				foreach (var config in appSettings.S3Locations)
				{
					var locationId = config.Key;
					string[] args = ["--endpoint-url", config.Value.Endpoint,
							"-P", locationId, "-b", backup.BackupId, config.Value.S3Path, dbId];
					await Terminal.Run("delete backup", "barman-cloud-backup-delete", args);
					await dbService.RemoveBackup(dbId, backup.BackupId, config.Key);
					await notify.RemoveBackup(dbId, backup.BackupId, locationId);
				}
			}
		}
	}
}
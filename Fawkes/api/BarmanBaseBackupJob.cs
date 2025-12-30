using Fawkes.Api.Settings;
using NodaTime;
using Quartz;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Fawkes.Api;

public class BarmanBackupResults
{
	[JsonPropertyName("backup_id")]
	public string? BackupId { get; set; }

	[JsonPropertyName("_ERROR")]
	public List<string> Errors { get; set; } = [];

	[JsonPropertyName("backup_name")]
	public string? Name { get; set; }
}

public class BarmanBaseBackupJob : IJob
{
	private readonly AppSettings appSettings;
	private readonly ArchiveService archiveService;
	private readonly IClock clock;
	private readonly DbService dbService;
	private readonly INotificationService notify;

	public BarmanBaseBackupJob(INotificationService notify, DbService dbService, AppSettings appSettings,
		ArchiveService archiveService, IClock clock)
	{
		this.notify = notify;
		this.dbService = dbService;
		this.appSettings = appSettings;
		this.archiveService = archiveService;
		this.clock = clock;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		if (context.MergedJobDataMap.TryGetString("dbId", out var dbId) && dbId != null)
		{
			context.MergedJobDataMap.TryGetString("name", out var name);
			await Start(dbId, name);
		}
	}

	public async Task Start(string dbId, string? name)
	{
		if (appSettings.Databases.TryGetValue(dbId, out var config))
		{
			var dbName = config.Name ?? dbId;
			var id = await notify.ServerMessage(new Notification($"Performing backup for {dbName}.") { DbId = dbId, Variant = MessageVariant.OnGoing });
			var args = new List<string> { "--format", "json", "--log-level", "INFO", "backup", "--wait" };
			if (!string.IsNullOrWhiteSpace(name))
			{
				args.Add("--name");
				args.Add(name);
			}
			args.Add(dbId);
			var result = await Terminal.RunBarman(args);
			var results = JsonSerializer.Deserialize<BarmanBackupResults>(result.StandardOutput) ?? new();
			if (results.Errors.Count > 0)
			{
				await notify.ClearServerMessage(id, results.Errors.First(), MessageVariant.Destructive);
			}
			else
			{
				if (results.BackupId != null)
				{
					var backups = new List<Backup>();
					var timestamp = clock.GetCurrentInstant();
					var archival = archiveService.DetermineAction(timestamp, dbId);
					foreach (var locationId in config.BackupLocations)
					{
						var newBackup = new Backup
						{
							BackupId = results.BackupId,
							ArchivalStatus = KeepStatus.nokeep,
							LocationId = locationId,
							Timestamp = timestamp,
							Archived = archival.Action == ArchiveAction.Keep,
							ArchivedUntil = archival.ExpirationDate
						};
						backups.Add(newBackup);
						await dbService.AddBackup(dbId, newBackup);
					}
					await notify.ClearServerMessage(id, $"Base backup complete for {dbName}.", MessageVariant.Success);
					await notify.AddBackupGroup(dbId, new BackupGroup
					{
						BackupId = results.BackupId,
						Backups = backups,
						Name = name
					});
				}
			}
		}
	}
}

public partial class InitDbBackup : IJob
{
	private readonly AppSettings appSettings;
	private readonly DbService dbService;
	private readonly INotificationService notify;
	private static readonly Regex errorRegex = ErrorRegex();

	public InitDbBackup(DbService dbService, AppSettings appSettings, INotificationService notify)
	{
		this.dbService = dbService;
		this.appSettings = appSettings;
		this.notify = notify;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		if (context.MergedJobDataMap.TryGetString("dbId", out var dbId) && dbId != null)
		{
			if (appSettings.Databases.TryGetValue(dbId, out var db))
			{
				List<Notification> errors = [];
				var notificationId = await notify.ServerMessage(new Notification($"Setting up {db.Name} WAL archiving")
				{
					DbId = db.Id,
					Variant = MessageVariant.OnGoing
				});
				var loadTask = dbService.LoadBackups(dbId);
				bool walSuccess;
				do
				{
					await Terminal.RunBarman("-q", "cron");
					var check = await Terminal.RunBarman("check", dbId);
					walSuccess = !check.StandardOutput.Contains("WAL archive: FAILED");
					List<string> iterationErrors = [];
					foreach (Match match in errorRegex.Matches(check.StandardOutput))
					{
						var error = match.Groups[0].Value;
						iterationErrors.Add(error);
						if (errors.All(err => err.Message != error))
						{
							var notification = new Notification(error) { DbId = dbId, Variant = MessageVariant.Destructive };
							errors.Add(notification);
							await notify.ServerMessage(notification);
						}
					}
					foreach (var clearedError in errors.Where(e => !iterationErrors.Contains(e.Message)).ToList())
					{
						await notify.ClearServerMessage(clearedError.Id);
						errors.Remove(clearedError);
					}
					if (!walSuccess)
					{
						await Task.Delay(TimeSpan.FromSeconds(10));
					}
				} while (!walSuccess);
				foreach (var error in errors)
				{
					await notify.ClearServerMessage(error.Id);
				}
				await notify.ClearServerMessage(notificationId, $"Database {db.Name} successfully set up.", MessageVariant.Success);
				await loadTask;
			}
		}
	}

	[GeneratedRegex(@"(?<=FATAL: ).+(?=\))", RegexOptions.Multiline)]
	private static partial Regex ErrorRegex();
}
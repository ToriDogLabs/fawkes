using Quartz;

namespace Fawkes.Api;

public class Constants
{
	public const string AwsConfigPath = $"{AwsDir}/config";
	public const string AwsCredentialsPath = $"{AwsDir}/credentials";
	public const string AwsDir = $"{HomeDir}/.aws";
	public const string BarmanConfigPath = $"/etc/barman.conf";
	public const string ConfigDir = $"{HomeDir}/config";
	public const string DbConfigPath = $"{ConfigDir}/db.conf";
	public const string HomeDir = "/app/data";
	public const string PgPassPath = $"/{HomeDir}/.pgpass";
	public const string QuartzSqliteConnection = $"Data Source={HomeDir}/quartz.db;Version=3;";
	public const string BackupsSqliteConnection = $"Data Source={HomeDir}/backups.db;Version=3;";
	public static readonly JobKey BarmanCleanupJobKey = new("barman cleanup", "cleanup");
	public static readonly JobKey BarmanCronJobKey = new("barman cron", "cron");
	public static string SettingsPath = $"/{HomeDir}/settings.json";

	public static string GetDbBackupScriptPath(string dbId) => $"{GetDbDir(dbId)}/cloud_backup.sh";

	public static string GetDbDir(string dbId) => $"{ConfigDir}/{dbId}";

	public static string GetDbWalScriptPath(string dbId) => $"{GetDbDir(dbId)}/cloud_wal_archive.sh";

	public static JobKey GetScheduleJobKey(string scheduleId) => new($"base_{scheduleId}", "cron");

	public static string GetSlotName(string dbId) => $"fawkes_{dbId.Replace("-", "_")}";
}
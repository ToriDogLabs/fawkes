using Fawkes.Api.Settings;
using NodaTime;
using System.Data.SQLite;

namespace Fawkes.Api;

public class DbService
{
	private readonly AppSettings appSettings;
	private readonly ArchiveService archiveService;

	public DbService(AppSettings appSettings, ArchiveService archiveService)
	{
		this.appSettings = appSettings;
		this.archiveService = archiveService;
	}

	public async Task AddBackup(string dbId, Backup backup)
	{
		await Command("INSERT INTO backups(dbId, backupId, archivalStatus, locationId, label, timestamp) VALUES(@dbId, @backupId, @status, @locationId, @label, @timestamp);")
			.AddParam("@dbId", dbId)
			.AddParam("@backupId", backup.BackupId)
			.AddParam("@status", backup.ArchivalStatus.ToString())
			.AddParam("@locationId", backup.LocationId)
			.AddParam("@label", backup.Name)
			.AddParam("@timestamp", backup.Timestamp.ToUnixTimeMilliseconds())
			.Execute();
	}

	public async Task Clear(string dbId)
	{
		await Command("DELETE FROM backups WHERE dbId = @dbId;")
			.AddParam("@dbId", dbId)
			.Execute();
	}

	public async Task<List<Backup>> GetBackups(string dbId)
	{
		return await Command("SELECT backupId, archivalStatus, locationId, label, timestamp FROM backups WHERE dbId = @dbId;")
			.AddParam("@dbId", dbId)
			.ExecuteQuery(r => ReadBackup(dbId, r));
	}

	public async Task<List<Backup>> GetNoKeepBackups(string dbId)
	{
		return await Command("SELECT backupId, archivalStatus, locationId, label, timestamp FROM backups WHERE dbId = @dbId AND archivalStatus = 'nokeep' ORDER BY timestamp desc;")
			.AddParam("@dbId", dbId)
			.ExecuteQuery(r => ReadBackup(dbId, r));
	}

	public async Task Init(AppSettings appSettings)
	{
		await Command(@"DROP TABLE IF EXISTS backups; CREATE TABLE backups(dbId text, backupId text, archivalStatus text, locationId text, label text, timestamp int);")
			.Execute();
		foreach (var dbId in appSettings.Databases.Keys)
		{
			await LoadBackups(dbId);
		}
	}

	public async Task LoadBackups(string dbId)
	{
		await Command("DELETE FROM backups WHERE dbId=@dbId;")
			.AddParam("@dbId", dbId)
			.Execute();
		foreach (var backup in await Barman.GetBackups(appSettings, dbId, archiveService))
		{
			await AddBackup(dbId, backup);
		}
	}

	public async Task RemoveBackup(string dbId, string backupId, string locationId)
	{
		await Command("DELETE FROM backups WHERE backupId=@backupId AND dbId=@dbId AND locationId=@locationId;")
			.AddParam("@backupId", backupId)
			.AddParam("@dbId", dbId)
			.AddParam("@locationId", locationId)
			.Execute();
	}

	public async Task UpdateStatus(string dbId, string backupId, string status, string locationId)
	{
		await Command("UPDATE backups SET archivalStatus = @status WHERE dbId = @dbId AND backupId = @backupId AND locationId = @locationId;")
			.AddParam("@status", status)
			.AddParam("@dbId", dbId)
			.AddParam("@backupId", backupId)
			.AddParam("@locationId", locationId)
			.Execute();
	}

	private static SqlBuilder Command(string sql)
	{
		return new SqlBuilder(sql);
	}

	private Backup ReadBackup(string dbId, SQLiteDataReader reader)
	{
		if (!Enum.TryParse<KeepStatus>(reader.GetString(1), out var status))
		{
			status = KeepStatus.nokeep;
		}
		var timestamp = Instant.FromUnixTimeMilliseconds(reader.GetInt64(4));
		var archival = archiveService.DetermineAction(timestamp, dbId);
		return new Backup()
		{
			BackupId = reader.GetString(0),
			ArchivalStatus = status,
			LocationId = reader.GetString(2),
			Name = reader.IsDBNull(3) ? null : reader.GetString(3),
			Timestamp = timestamp,
			Archived = archival.Action == ArchiveAction.Keep,
			ArchivedUntil = archival.ExpirationDate
		};
	}
}

public class SqlBuilder
{
	private readonly List<SQLiteParameter> parameters = [];
	private readonly string sql;

	public SqlBuilder(string sql)
	{
		this.sql = sql;
	}

	public SqlBuilder AddParam(string name, string? value)
	{
		parameters.Add(new SQLiteParameter(name, System.Data.DbType.String)
		{
			Value = value
		});
		return this;
	}

	public SqlBuilder AddParam(string name, long value)
	{
		parameters.Add(new SQLiteParameter(name, System.Data.DbType.Int64)
		{
			Value = value
		});
		return this;
	}

	public SqlBuilder AddParam(string name, bool value)
	{
		parameters.Add(new SQLiteParameter(name, System.Data.DbType.Boolean)
		{
			Value = value
		});
		return this;
	}

	public async Task Execute()
	{
		using var connection = await GetConnection();
		using var command = connection.CreateCommand();
		command.CommandText = sql;
		command.Parameters.AddRange([.. parameters]);
		await command.ExecuteNonQueryAsync();
	}

	public async Task<List<T>> ExecuteQuery<T>(Func<SQLiteDataReader, T> read)
	{
		using var connection = await GetConnection();
		using var command = connection.CreateCommand();
		command.CommandText = sql;
		command.Parameters.AddRange([.. parameters]);
		using var reader = command.ExecuteReader();
		var rows = new List<T>();
		while (await reader.ReadAsync())
		{
			rows.Add(read(reader));
		}
		return rows;
	}

	private async Task<SQLiteConnection> GetConnection()
	{
		if (!Directory.Exists(Constants.HomeDir))
		{
			Directory.CreateDirectory(Constants.HomeDir);
		}
		var connection = new SQLiteConnection(Constants.BackupsSqliteConnection);
		await connection.OpenAsync();
		return connection;
	}
}
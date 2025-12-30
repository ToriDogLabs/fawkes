using Fawkes.Api.Settings;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Quartz;
using Quartz.Impl.Matchers;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Voyager;

namespace Fawkes.Api.Endpoints;

[VoyagerEndpoint("/db/{dbId}")]
public partial class Db
{
	private static Regex pgPassRegex = PgPassRegex();

	private static Regex versionRegex = VersionRegex();

	public static void Configure(RouteHandlerBuilder builder)
	{
		builder.WithOpenApi();
	}

	public static NpgsqlConnectionStringBuilder CreateConnectionString(string host, string username,
		string password, bool useSsl, string database, string? portString)
	{
		if (!int.TryParse(portString, out var port))
		{
			port = 5432;
		}
		return new NpgsqlConnectionStringBuilder
		{
			Host = host,
			Username = username,
			Password = password,
			SslMode = useSsl ? SslMode.Allow : SslMode.Disable,
			Database = database,
			Port = port,
		};
	}

	public static async Task<IResult> Delete(DeleteRequest request, AppSettings appSettings,
		ISchedulerFactory schedulerFactory, DbService dbService)
	{
		if (!appSettings.Databases.TryGetValue(request.dbId, out var db))
		{
			return TypedResults.NotFound();
		}

		var scheduler = await schedulerFactory.GetScheduler();
		var dbId = request.dbId;

		await Terminal.RunBarman("-q", "cron");

		await File.WriteAllLinesAsync(Constants.PgPassPath, await GetPgPassWithoutBarman(db.Host));

		var dbConfigDir = Constants.GetDbDir(dbId);
		if (Directory.Exists(dbConfigDir))
		{
			Directory.Delete(dbConfigDir, true);
		}
		foreach (var lockFile in Directory.GetFiles(Constants.HomeDir, $".{dbId}-*.lock"))
		{
			File.Delete(lockFile);
		}
		await dbService.Clear(dbId);

		foreach (var schedule in appSettings.Databases[dbId].BackupSchedule)
		{
			var jobKey = Constants.GetScheduleJobKey(schedule.Id);
			foreach (var key in await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(jobKey.Group)))
			{
				if (key.Name == jobKey.Name)
				{
					await scheduler.DeleteJob(key);
				}
			}
		}
		appSettings.Databases.Remove(request.dbId);
		await appSettings.Save();
		return TypedResults.Ok();
	}

	public static async Task<IResult> Post(PostRequest request, AppSettings appSettings, ISchedulerFactory schedulerFactory)
	{
		try
		{
			var scheduler = await schedulerFactory.GetScheduler();
			var database = await Configure(request.DbId, request.Config, scheduler, appSettings);
			if (database != null)
			{
				return TypedResults.Ok(database);
			}
		}
		catch (SocketException e)
		{
			return TypedResults.Problem(title: e.Message);
		}
		catch (Exception e)
		{
			return TypedResults.Problem(title: e.Message);
		}
		return TypedResults.Problem(title: "Error adding database.");
	}

	private static async Task<DatabaseSettings?> Configure(string dbId, DbConfig configuration,
		IScheduler scheduler, AppSettings appSettings)
	{
		if (string.IsNullOrWhiteSpace(configuration.user))
		{
			throw new Exception("User must be set");
		}
		await using var dbConn = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder
		{
			Host = configuration.host,
			Port = int.Parse(configuration.port ?? "5432"),
			Username = configuration.user,
			Password = configuration.password,
			SslMode = (configuration.useSsl ?? false) ? SslMode.Require : SslMode.Disable,
			Database = configuration.database
		});
		await using var versionQuery = dbConn.CreateCommand("SELECT VERSION();");
		await using var reader = await versionQuery.ExecuteReaderAsync();
		await reader.ReadAsync();
		var versionString = reader.GetString(0);
		var match = versionRegex.Match(versionString);
		if (!match.Success || !double.TryParse(match.Groups[1].Value, out var version))
		{
			return null;
		}
		var databaseSettings = new DatabaseSettings
		{
			Id = dbId,
			Database = configuration.database,
			Host = configuration.host,
			ReplicationPassword = configuration.password,
			Name = configuration.name,
			ReplicationUser = configuration.user,
			Port = configuration.port,
			UseSsl = configuration.useSsl ?? false
		};
		await UpdatePgPass(configuration.host, configuration.password, databaseSettings);
		appSettings.Databases.Add(dbId, databaseSettings);
		await appSettings.Save();

		await Terminal.RunBarman("-q", "cron");
		await Terminal.RunBarman("check", dbId);
		await Terminal.RunBarman("switch-wal", dbId);

		await scheduler.ScheduleJob(
			JobBuilder.Create<InitDbBackup>().UsingJobData("dbId", dbId).Build(),
			TriggerBuilder.Create().StartNow().Build());
		return databaseSettings;
	}

	private static string CreatePgPassLine(string host, string username, string password)
	{
		return $"{host}:*:*:{username}:{password.Replace("\\", "\\\\").Replace(":", "\\:")}";
	}

	private static async Task<List<string>> GetPgPassWithoutBarman(string host)
	{
		var lines = await File.ReadAllLinesAsync(Constants.PgPassPath);
		return lines.Where(l =>
		{
			var match = pgPassRegex.Match(l);
			var lineUsername = match.Groups["username"].Value;
			return match.Groups["host"].Value != host || (lineUsername != "barman" && !lineUsername.StartsWith("barman_replica"));
		}).ToList();
	}

	[GeneratedRegex("(?<host>[^:]+?):([^:]+?):([^:]+?):(?<username>[^:]+?):([^:]+)")]
	private static partial Regex PgPassRegex();

	private static async Task UpdatePgPass(string host, string password, DatabaseSettings settings)
	{
		var output = await GetPgPassWithoutBarman(host);
		output.Add(CreatePgPassLine(host, settings.ReplicationUser, password));
		await File.WriteAllLinesAsync(Constants.PgPassPath, output);
	}

	[GeneratedRegex(@"PostgreSQL (\d+.\d+)", RegexOptions.Compiled)]
	private static partial Regex VersionRegex();

	public record PostRequest([FromBody] DbConfig Config, [FromRoute] string DbId);
	public record DeleteRequest([FromRoute] string dbId);
}
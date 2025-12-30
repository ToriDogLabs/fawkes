using Fawkes.Api;
using Fawkes.Api.Settings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Any;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Quartz;
using Scalar.AspNetCore;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Tools;
using Voyager.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
	ConfigureJsonSerializers(options.SerializerOptions);
});

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddVoyager();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(c =>
{
	c.AddVoyager();
	c.AddDocumentTransformer((document, context, cancellationToken) =>
	{
		if (document?.Components?.Schemas != null)
		{
			foreach (var schema in document.Components.Schemas)
			{
				var openApiSchema = schema.Value;
				if (openApiSchema.Enum is { Count: > 0 } && openApiSchema.Type == "integer")
				{
					openApiSchema.Type = "string";
					openApiSchema.Format = null;

					openApiSchema.Enum = openApiSchema.Enum
						.OfType<OpenApiInteger>()
						.Select(e => new OpenApiString(e.Value.ToString()))
						.Cast<IOpenApiAny>()
						.ToList();
				}
			}
		}
		return Task.CompletedTask;
	});
	c.AddSchemaTransformer((schema, context, _) =>
	{
		if (context.JsonTypeInfo.Type == typeof(NodaTime.Instant))
		{
			schema.Type = "string";
			schema.Format = "date-time";
		}
		if (context.JsonTypeInfo.Type == typeof(NodaTime.Instant?))
		{
			schema.Type = "string";
			schema.Nullable = true;
			schema.Format = "date-time";
		}
		return Task.CompletedTask;
	});
});
builder.Services.AddQuartz(config =>
{
	config.UsePersistentStore(store =>
	{
		store.UseSQLite(sqlite =>
		{
			sqlite.ConnectionString = Constants.QuartzSqliteConnection;
		});
		store.UseSystemTextJsonSerializer();
	});
});

builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton<DbService>();
builder.Services.AddTransient<ArchiveService>();
builder.Services.AddSingleton<AppSettings>((sp) =>
{
	var factory = sp.GetRequiredService<ISchedulerFactory>();
	return AppSettings.Load(factory);
});
builder.Services.AddQuartzHostedService(opt =>
{
	opt.WaitForJobsToComplete = true;
});
builder.Services.AddHealthChecks();
builder.Services.AddCors(o =>
{
	o.AddDefaultPolicy(p =>
	{
		p.SetIsOriginAllowed(o => true).AllowCredentials().AllowAnyHeader().AllowAnyMethod();
	});
});
builder.Services.AddSignalR()
	.AddJsonProtocol(options =>
	{
		ConfigureJsonSerializers(options.PayloadSerializerOptions);
	});
builder.Services.AddSingleton<INotificationService, WebsocketNotificationService>();
builder.Services.AddTransient<IWebsocketClientProvider, WebsocketClientProvider>();

var app = builder.Build();

var dbService = app.Services.GetRequiredService<DbService>();
await InitSql(dbService);

app.UseCors();
var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(wwwrootPath))
{
	app.UseStaticFiles(new StaticFileOptions
	{
		FileProvider = new PhysicalFileProvider(wwwrootPath)
	});
}
app.UseRouting();
app.UseEndpoints(endpoints =>
{
	_ = endpoints.MapFallbackToFile("index.html");
});
app.MapVoyager();
app.MapOpenApi();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapScalarApiReference();
}

var factory = app.Services.GetRequiredService<ISchedulerFactory>();
var scheduler = await factory.GetScheduler();
var appSettings = app.Services.GetRequiredService<AppSettings>();
await dbService.Init(appSettings);
await appSettings.Save();

var barmanConfig = new IniDocument();
var barmanSection = barmanConfig.AddSection("barman");
barmanSection.Add("barman_user", "barman");
barmanSection.Add("barman_home", Constants.HomeDir);
barmanSection.Add("configuration_files_directory", Constants.ConfigDir);
barmanSection.Add("log_level", "DEBUG");
barmanSection.Add("retention_policy_mode", "auto");
barmanSection.Add("wal_retention_policy", "main");
barmanSection.Add("log_file", "/var/log/barman/barman.log");
await barmanConfig.WriteToFile(Constants.BarmanConfigPath);

if (!Directory.Exists(Constants.ConfigDir))
{
	Directory.CreateDirectory(Constants.ConfigDir);

	var pgPassPath = $"{Constants.HomeDir}/.pgpass";
	CreateFile(pgPassPath);
	if (OperatingSystem.IsLinux())
	{
		File.SetUnixFileMode(pgPassPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
	}
	CreateFile($"{Constants.HomeDir}/.barman.auto.conf");
}

var numberRegex = NumberRegex();
var retentionUnitsRegex = RetentionUnitsRegex();

if (!await scheduler.CheckExists(Constants.BarmanCronJobKey))
{
	var barmanCronJob = JobBuilder.Create<BarmanCronJob>().WithIdentity(Constants.BarmanCronJobKey).Build();
	var barmanCronTrigger = TriggerBuilder.Create().StartNow().WithSimpleSchedule(x => x.WithIntervalInMinutes(1).RepeatForever()).Build();
	await scheduler.ScheduleJob(barmanCronJob, barmanCronTrigger);
}
await ArchiveService.UpdateCleanupJob(appSettings, scheduler);

app.MapGet("/db", () =>
{
	return appSettings.Databases.Select(dbKvp =>
	{
		try
		{
			var db = dbKvp.Value;
			return new
			{
				Id = dbKvp.Key,
				Db = db
			};
		}
		catch
		{
			return null;
		}
	}).Where(v => v != null);
});

var hostRegex = HostRegex();
var pgUserRegex = PgUserRegex();

app.MapPut("/db/{dbId}/backup/{backupId}/keep", async ([FromRoute] string backupId, [FromRoute] string dbId, [FromBody] MultiLocationRequest req) =>
{
	Dictionary<string, KeepStatus> results = [];
	foreach (var locationId in req.LocationIds)
	{
		if (locationId == "local")
		{
			var result = await Terminal.RunBarman("keep", "--target", "full", dbId, backupId);
			var archivalStatus = await Terminal.GetLocalStatus(dbId, backupId);
			await dbService.UpdateStatus(dbId, backupId, archivalStatus.ToString(), locationId);
			results[locationId] = archivalStatus;
		}
		else
		{
			if (appSettings.S3Locations.TryGetValue(locationId, out var config))
			{
				var result = await Terminal.Run("keep", "barman-cloud-backup-keep", "--endpoint-url", config.Endpoint,
					"-P", locationId, "--target", "full", config.S3Path, dbId, backupId);
				var archivalStatus = await Terminal.GetArchivalStatus(config, dbId, backupId);
				await dbService.UpdateStatus(dbId, backupId, archivalStatus.ToString(), locationId);
				results[locationId] = archivalStatus;
			}
		}
	}
	return results;
});

app.MapDelete("/db/{dbId}/backup/{backupId}/keep", async ([FromRoute] string backupId, [FromRoute] string dbId, [FromBody] MultiLocationRequest req) =>
{
	Dictionary<string, KeepStatus> results = [];
	foreach (var locationId in req.LocationIds)
	{
		if (locationId == "local")
		{
			var output = await Terminal.RunBarman("keep", "--release", dbId, backupId);
			var archivalStatus = await Terminal.GetLocalStatus(dbId, backupId);
			await dbService.UpdateStatus(dbId, backupId, archivalStatus.ToString(), locationId);
			results[locationId] = archivalStatus;
		}
		else
		{
			if (appSettings.S3Locations.TryGetValue(locationId, out var config))
			{
				await Terminal.Run("delete keep", "barman-cloud-backup-keep", "--endpoint-url", config.Endpoint,
					"-P", locationId, "-r", config.S3Path, dbId, backupId);
				var archivalStatus = await Terminal.GetArchivalStatus(config, dbId, backupId);
				await dbService.UpdateStatus(dbId, backupId, archivalStatus.ToString(), locationId);
				results[locationId] = archivalStatus;
			}
		}
	}
	return results;
});

app.MapDelete("/db/{dbId}/backup/{backupId}", async ([FromRoute] string backupId, [FromRoute] string dbId,
	[FromBody] MultiLocationRequest req) =>
{
	var deletedLocations = new List<string>();
	foreach (var locationId in req.LocationIds)
	{
		if (locationId == "local")
		{
			await Terminal.RunBarman("delete", dbId, backupId);
			await dbService.RemoveBackup(dbId, backupId, "local");
			deletedLocations.Add(locationId);
		}
		else
		{
			if (appSettings.S3Locations.TryGetValue(locationId, out var config))
			{
				await Terminal.Run("delete backup", "barman-cloud-backup-delete", "--endpoint-url", config.Endpoint,
					"-P", locationId, "-b", backupId, config.S3Path, dbId);
				await dbService.RemoveBackup(dbId, backupId, locationId);
				deletedLocations.Add(locationId);
			}
		}
	}
	return deletedLocations;
});

app.MapGet("/db/{dbId}/schedule", ([FromRoute] string dbId) =>
{
	if (appSettings.Databases.TryGetValue(dbId, out var dbSettings))
	{
		return dbSettings.BackupSchedule;
	}
	return Enumerable.Empty<DbBackupSchedule>();
});

app.MapPut("/db/{dbId}/schedule", async ([FromBody] DbBackupSchedule request, [FromRoute] string dbId) =>
{
	var jobKey = Constants.GetScheduleJobKey(request.Id);
	var triggers = await scheduler.GetTriggersOfJob(jobKey);
	var cronTrigger = triggers.OfType<ICronTrigger>().FirstOrDefault();
	if (cronTrigger != null)
	{
		var newTrigger = TriggerBuilder.Create()
			.WithIdentity(cronTrigger.Key)
			.WithSchedule(CronScheduleBuilder.CronSchedule(request.CronSchedule))
			.ForJob(cronTrigger.JobKey)
			.StartAt(cronTrigger.StartTimeUtc)
			.UsingJobData("type", request.Type.ToString())
			.UsingJobData("dbId", dbId)
			.Build();
		await scheduler.RescheduleJob(cronTrigger.Key, newTrigger);
	}
	else
	{
		await scheduler.ScheduleJob(
			JobBuilder.Create<BarmanBaseBackupJob>()
				.WithIdentity(jobKey)
				.UsingJobData("dbId", dbId)
				.UsingJobData("type", request.Type.ToString())
				.Build(),
			TriggerBuilder.Create()
				.WithIdentity(request.Id, $"cron.{dbId}")
				.WithSchedule(CronScheduleBuilder.CronSchedule(request.CronSchedule))
				.ForJob(jobKey)
				.StartNow()
				.Build()
		);
	}
	if (appSettings.Databases.TryGetValue(dbId, out var dbSettings))
	{
		var index = dbSettings.BackupSchedule.FindIndex(b => b.Id == request.Id);
		if (index >= 0)
		{
			dbSettings.BackupSchedule[index] = request;
		}
		else
		{
			dbSettings.BackupSchedule.Add(request);
		}
		await appSettings.Save();
	}
});

app.MapDelete("/db/{dbId}/schedule/{id}", async ([FromRoute] string dbId, [FromRoute] string id) =>
{
	if (appSettings.Databases.TryGetValue(dbId, out var dbSettings))
	{
		dbSettings.BackupSchedule.RemoveAll(s => s.Id == id);
		await appSettings.Save();
	}
});

app.MapPost("/db/{dbId}/backup", async (HttpContext context, [FromRoute] string dbId, [FromQuery] string? name) =>
{
	await scheduler.ScheduleJob(JobBuilder.Create<BarmanBaseBackupJob>()
		.UsingJobData("dbId", dbId)
		.UsingJobData("name", name)
		.Build(),
		TriggerBuilder.Create().StartNow().Build());
}).WithOpenApi();

app.MapGet("/db/{dbId}/backup", async ([FromRoute] string dbId) =>
{
	var backups = await dbService.GetBackups(dbId);
	return backups.GroupBy(b => b.BackupId).Select(group =>
	{
		return new BackupGroup
		{
			BackupId = group.Key,
			Backups = group,
			Name = group.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.Name))?.Name,
			ArchivedUntil = group.FirstOrDefault(b => b.ArchivedUntil != null)?.ArchivedUntil,
			Timestamp = group.First().Timestamp
		};
	});
}).WithOpenApi();

app.MapPost("/db/{dbId}/s3", async ([FromRoute] string dbId, [FromBody] string[] s3Ids) =>
{
	if (appSettings.Databases.TryGetValue(dbId, out var db))
	{
		db.BackupLocations = [.. s3Ids.Distinct()];
		await appSettings.Save();
	}
}).WithOpenApi();

app.MapGet("/db/{dbId}/backup/{backupId}/recover/{s3Id}", ([FromRoute] string dbId, [FromRoute] string backupId, [FromRoute] string s3Id, [FromQuery] string? targetTime) =>
{
	if (appSettings.S3Locations.TryGetValue(s3Id, out var config))
	{
		var script = new StringBuilder("fawkes_restore restore");
		script.Append($" --access_key {config.AccessKey}");
		script.Append($" --secret_key {config.SecretKey}");
		script.Append($" --backupId {backupId}");
		script.Append($" --endpoint {config.Endpoint}");
		script.Append($" --path {config.S3Path.Replace("s3://", "")}/{dbId}");
		if (!string.IsNullOrWhiteSpace(targetTime))
		{
			script.Append($" --target_time {targetTime}");
		}
		script.Append($" --recover_dir");

		return new
		{
			script = script.ToString()
		};
	}
	return null;
}).WithOpenApi();

app.MapGet("/retention/{dbId}", ([FromRoute] string dbId) =>
{
	if (appSettings.Databases.TryGetValue(dbId, out var db))
	{
		return db.RetentionPolicy;
	}
	return null;
}).WithOpenApi();

app.MapPut("/retention/{dbId}", async Task<Results<Ok, NotFound>> ([FromRoute] string dbId, [FromBody] RetentionPolicy policy) =>
{
	if (appSettings.Databases.TryGetValue(dbId, out var db))
	{
		db.RetentionPolicy = policy;
		await appSettings.Save();
		return TypedResults.Ok();
	}
	return TypedResults.NotFound();
}).WithOpenApi();

app.MapDelete("/db/{dbId}/archive/policy/{id}", async Task<Results<Ok, ProblemHttpResult>>
	([FromRoute] string dbId, [FromRoute] Guid id) =>
{
	if (appSettings.Databases.TryGetValue(dbId, out var db))
	{
		db.ArchivalPolicies.RemoveAll(p => p.Id == id);
		await appSettings.Save();
		return TypedResults.Ok();
	}
	return TypedResults.Problem("Unable to find database.");
}).WithOpenApi();

app.MapPut("/db/{dbId}/archive/policy", async Task<Results<Ok, ProblemHttpResult>>
	([FromRoute] string dbId, [FromBody] ArchivalPolicy policy) =>
{
	if (appSettings.Databases.TryGetValue(dbId, out var db))
	{
		var index = db.ArchivalPolicies.FindIndex(p => p.Id == policy.Id);
		if (index >= 0)
		{
			db.ArchivalPolicies[index] = policy;
		}
		else
		{
			db.ArchivalPolicies.Add(policy);
		}
		await appSettings.Save();
		return TypedResults.Ok();
	}
	return TypedResults.Problem("Unable to find database.");
}).WithOpenApi();

app.MapGet("/db{dbId}/archive/policies", Ok<List<ArchivalPolicy>>
	([FromRoute] string dbId) =>
{
	List<ArchivalPolicy> policies = [];
	if (appSettings.Databases.TryGetValue(dbId, out var db))
	{
		policies = db.ArchivalPolicies;
	}
	return TypedResults.Ok(policies);
}).WithOpenApi();

app.MapHub<SignalrHub>("/signalr");
app.MapHealthChecks("/health");

app.Run();

static async Task InitSql(DbService dbService)
{
	var createTableSql = @"
CREATE TABLE IF NOT EXISTS QRTZ_JOB_DETAILS
  (
    SCHED_NAME NVARCHAR(120) NOT NULL,
	JOB_NAME NVARCHAR(150) NOT NULL,
    JOB_GROUP NVARCHAR(150) NOT NULL,
    DESCRIPTION NVARCHAR(250) NULL,
    JOB_CLASS_NAME   NVARCHAR(250) NOT NULL,
    IS_DURABLE BIT NOT NULL,
    IS_NONCONCURRENT BIT NOT NULL,
    IS_UPDATE_DATA BIT  NOT NULL,
	REQUESTS_RECOVERY BIT NOT NULL,
    JOB_DATA BLOB NULL,
    PRIMARY KEY (SCHED_NAME,JOB_NAME,JOB_GROUP)
);

CREATE TABLE IF NOT EXISTS QRTZ_TRIGGERS
  (
    SCHED_NAME NVARCHAR(120) NOT NULL,
	TRIGGER_NAME NVARCHAR(150) NOT NULL,
    TRIGGER_GROUP NVARCHAR(150) NOT NULL,
    JOB_NAME NVARCHAR(150) NOT NULL,
    JOB_GROUP NVARCHAR(150) NOT NULL,
    DESCRIPTION NVARCHAR(250) NULL,
    NEXT_FIRE_TIME BIGINT NULL,
    PREV_FIRE_TIME BIGINT NULL,
    PRIORITY INTEGER NULL,
    TRIGGER_STATE NVARCHAR(16) NOT NULL,
    TRIGGER_TYPE NVARCHAR(8) NOT NULL,
    START_TIME BIGINT NOT NULL,
    END_TIME BIGINT NULL,
    CALENDAR_NAME NVARCHAR(200) NULL,
    MISFIRE_INSTR INTEGER NULL,
    JOB_DATA BLOB NULL,
    PRIMARY KEY (SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP),
    FOREIGN KEY (SCHED_NAME,JOB_NAME,JOB_GROUP)
        REFERENCES QRTZ_JOB_DETAILS(SCHED_NAME,JOB_NAME,JOB_GROUP)
);

CREATE TABLE IF NOT EXISTS QRTZ_SIMPLE_TRIGGERS
  (
    SCHED_NAME NVARCHAR(120) NOT NULL,
	TRIGGER_NAME NVARCHAR(150) NOT NULL,
    TRIGGER_GROUP NVARCHAR(150) NOT NULL,
    REPEAT_COUNT BIGINT NOT NULL,
    REPEAT_INTERVAL BIGINT NOT NULL,
    TIMES_TRIGGERED BIGINT NOT NULL,
    PRIMARY KEY (SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP),
    FOREIGN KEY (SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP)
        REFERENCES QRTZ_TRIGGERS(SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP) ON DELETE CASCADE
);

CREATE TRIGGER IF NOT EXISTS  DELETE_SIMPLE_TRIGGER DELETE ON QRTZ_TRIGGERS
BEGIN
	DELETE FROM QRTZ_SIMPLE_TRIGGERS WHERE SCHED_NAME=OLD.SCHED_NAME AND TRIGGER_NAME=OLD.TRIGGER_NAME AND TRIGGER_GROUP=OLD.TRIGGER_GROUP;
END
;

CREATE TABLE IF NOT EXISTS QRTZ_SIMPROP_TRIGGERS
  (
    SCHED_NAME NVARCHAR (120) NOT NULL ,
    TRIGGER_NAME NVARCHAR (150) NOT NULL ,
    TRIGGER_GROUP NVARCHAR (150) NOT NULL ,
    STR_PROP_1 NVARCHAR (512) NULL,
    STR_PROP_2 NVARCHAR (512) NULL,
    STR_PROP_3 NVARCHAR (512) NULL,
    INT_PROP_1 INT NULL,
    INT_PROP_2 INT NULL,
    LONG_PROP_1 BIGINT NULL,
    LONG_PROP_2 BIGINT NULL,
    DEC_PROP_1 NUMERIC NULL,
    DEC_PROP_2 NUMERIC NULL,
    BOOL_PROP_1 BIT NULL,
    BOOL_PROP_2 BIT NULL,
    TIME_ZONE_ID NVARCHAR(80) NULL,
	PRIMARY KEY (SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP),
	FOREIGN KEY (SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP)
        REFERENCES QRTZ_TRIGGERS(SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP) ON DELETE CASCADE
);

CREATE TRIGGER IF NOT EXISTS  DELETE_SIMPROP_TRIGGER DELETE ON QRTZ_TRIGGERS
BEGIN
	DELETE FROM QRTZ_SIMPROP_TRIGGERS WHERE SCHED_NAME=OLD.SCHED_NAME AND TRIGGER_NAME=OLD.TRIGGER_NAME AND TRIGGER_GROUP=OLD.TRIGGER_GROUP;
END
;

CREATE TABLE IF NOT EXISTS QRTZ_CRON_TRIGGERS
  (
    SCHED_NAME NVARCHAR(120) NOT NULL,
	TRIGGER_NAME NVARCHAR(150) NOT NULL,
    TRIGGER_GROUP NVARCHAR(150) NOT NULL,
    CRON_EXPRESSION NVARCHAR(250) NOT NULL,
    TIME_ZONE_ID NVARCHAR(80),
    PRIMARY KEY (SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP),
    FOREIGN KEY (SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP)
        REFERENCES QRTZ_TRIGGERS(SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP) ON DELETE CASCADE
);

CREATE TRIGGER IF NOT EXISTS  DELETE_CRON_TRIGGER DELETE ON QRTZ_TRIGGERS
BEGIN
	DELETE FROM QRTZ_CRON_TRIGGERS WHERE SCHED_NAME=OLD.SCHED_NAME AND TRIGGER_NAME=OLD.TRIGGER_NAME AND TRIGGER_GROUP=OLD.TRIGGER_GROUP;
END
;

CREATE TABLE IF NOT EXISTS QRTZ_BLOB_TRIGGERS
  (
    SCHED_NAME NVARCHAR(120) NOT NULL,
	TRIGGER_NAME NVARCHAR(150) NOT NULL,
    TRIGGER_GROUP NVARCHAR(150) NOT NULL,
    BLOB_DATA BLOB NULL,
    PRIMARY KEY (SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP),
    FOREIGN KEY (SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP)
        REFERENCES QRTZ_TRIGGERS(SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP) ON DELETE CASCADE
);

CREATE TRIGGER IF NOT EXISTS  DELETE_BLOB_TRIGGER DELETE ON QRTZ_TRIGGERS
BEGIN
	DELETE FROM QRTZ_BLOB_TRIGGERS WHERE SCHED_NAME=OLD.SCHED_NAME AND TRIGGER_NAME=OLD.TRIGGER_NAME AND TRIGGER_GROUP=OLD.TRIGGER_GROUP;
END
;

CREATE TABLE IF NOT EXISTS QRTZ_CALENDARS
  (
    SCHED_NAME NVARCHAR(120) NOT NULL,
	CALENDAR_NAME  NVARCHAR(200) NOT NULL,
    CALENDAR BLOB NOT NULL,
    PRIMARY KEY (SCHED_NAME,CALENDAR_NAME)
);

CREATE TABLE IF NOT EXISTS QRTZ_PAUSED_TRIGGER_GRPS
  (
    SCHED_NAME NVARCHAR(120) NOT NULL,
	TRIGGER_GROUP NVARCHAR(150) NOT NULL,
    PRIMARY KEY (SCHED_NAME,TRIGGER_GROUP)
);

CREATE TABLE IF NOT EXISTS QRTZ_FIRED_TRIGGERS
  (
    SCHED_NAME NVARCHAR(120) NOT NULL,
	ENTRY_ID NVARCHAR(140) NOT NULL,
    TRIGGER_NAME NVARCHAR(150) NOT NULL,
    TRIGGER_GROUP NVARCHAR(150) NOT NULL,
    INSTANCE_NAME NVARCHAR(200) NOT NULL,
    FIRED_TIME BIGINT NOT NULL,
    SCHED_TIME BIGINT NOT NULL,
	PRIORITY INTEGER NOT NULL,
    STATE NVARCHAR(16) NOT NULL,
    JOB_NAME NVARCHAR(150) NULL,
    JOB_GROUP NVARCHAR(150) NULL,
    IS_NONCONCURRENT BIT NULL,
    REQUESTS_RECOVERY BIT NULL,
    PRIMARY KEY (SCHED_NAME,ENTRY_ID)
);

CREATE TABLE IF NOT EXISTS QRTZ_SCHEDULER_STATE
  (
    SCHED_NAME NVARCHAR(120) NOT NULL,
	INSTANCE_NAME NVARCHAR(200) NOT NULL,
    LAST_CHECKIN_TIME BIGINT NOT NULL,
    CHECKIN_INTERVAL BIGINT NOT NULL,
    PRIMARY KEY (SCHED_NAME,INSTANCE_NAME)
);

CREATE TABLE IF NOT EXISTS QRTZ_LOCKS
  (
    SCHED_NAME NVARCHAR(120) NOT NULL,
	LOCK_NAME  NVARCHAR(40) NOT NULL,
    PRIMARY KEY (SCHED_NAME,LOCK_NAME)
);";

	using var connection = new System.Data.SQLite.SQLiteConnection(Constants.QuartzSqliteConnection);
	await connection.OpenAsync();
	using var command = connection.CreateCommand();
	command.CommandText = createTableSql;
	await command.ExecuteNonQueryAsync();
}

static void CreateFile(string path)
{
	var directory = Path.GetDirectoryName(path);
	if (directory != null && !Directory.Exists(directory))
	{
		Directory.CreateDirectory(directory);
	}
	using var _ = File.Create(path);
}


static void ConfigureJsonSerializers(JsonSerializerOptions options)
{
	options.Converters.Add(new JsonStringEnumConverter());
	options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
}

public enum BackupType
{
	full,
	incremental
}

public enum KeepStatus
{
	standalone,
	full,
	nokeep
}

public enum RetentionUnits
{
	DAYS,
	WEEKS,
	MONTHS
}

public class Backup
{
	public required KeepStatus ArchivalStatus { get; init; }
	public required bool Archived { get; set; }
	public Instant? ArchivedUntil { get; set; }
	public required string BackupId { get; init; }
	public required string LocationId { get; init; }
	public string? Name { get; set; }
	public required Instant Timestamp { get; set; }
}

public class BackupGroup
{
	public Instant? ArchivedUntil { get; set; }
	public required string BackupId { get; set; }
	public required IEnumerable<Backup> Backups { get; set; }
	public string? Name { get; set; }
	public Instant Timestamp { get; set; }
}

public class BackupRequest
{
	public string? Name { get; init; }
}

public class OptionalResult<T> : OptionalResult
{
	public T? Result { get; set; }
}

public class OptionalResult
{
	public ProblemDetails? Error { get; set; }
}

public class RecoverCmdRequest
{
	public required string BackupId { get; init; }
	public required string Server { get; init; }
	public string? TargetTime { get; init; }
}

public class RetentionPolicy
{
	public required int MinimumRedundancy { get; init; }
	public required int Retention { get; init; }
	public required RetentionUnits RetentionUnits { get; init; }
}

internal partial class Program
{
	[GeneratedRegex(@"host=\s*(?<host>.+?)\b")]
	private static partial Regex HostRegex();

	[GeneratedRegex("[0-9]+")]
	private static partial Regex NumberRegex();

	[GeneratedRegex(@"pg_user=\s*(?<pgUser>.+?)\b")]
	private static partial Regex PgUserRegex();

	[GeneratedRegex("(days|weeks|months)", RegexOptions.IgnoreCase, "en-US")]
	private static partial Regex RetentionUnitsRegex();
}

public record MultiLocationRequest(List<string> LocationIds);
public record DbConfig(string host, string user, string password, string database, string name, string? port, bool? useSsl);

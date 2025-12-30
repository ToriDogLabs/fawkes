using Fawkes.Api.Settings;
using NodaTime;
using NodaTime.TimeZones;
using Quartz;
using System.Runtime.InteropServices;

namespace Fawkes.Api;

public enum ArchiveAction
{
	Keep,
	None,
}

public enum DurationUnits
{
	Day,
	Week,
	Month,
	Year
}

public enum RetentionStrategyType
{
	DayOfWeek,
	TargetDay
};

public class ArchivalPolicy
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public required ArchiveRetentionPolicy Retention { get; init; }
	public required RetentionStrategy Strategy { get; init; }
}

public class ArchiveRetentionPolicy
{
	public int Duration { get; init; }
	public DurationUnits Units { get; init; }
}

public class ArchiveService
{
	private readonly AppSettings appSettings;
	private readonly IClock clock;

	public ArchiveService(AppSettings appSettings, IClock clock)
	{
		this.appSettings = appSettings;
		this.clock = clock;
	}

	public static TimeZoneInfo GetTimeZone(DateTimeZone timeZone)
	{
		var zoneId = timeZone.Id;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var mapping = TzdbDateTimeZoneSource.Default.WindowsMapping.MapZones
				.FirstOrDefault(x => x.TzdbIds.Contains(zoneId));

			if (mapping != null)
			{
				zoneId = mapping.WindowsId;
			}
		}
		return TimeZoneInfo.FindSystemTimeZoneById(zoneId);
	}

	public static async Task UpdateCleanupJob(AppSettings appSettings, IScheduler scheduler)
	{
		var triggers = await scheduler.GetTriggersOfJob(Constants.BarmanCleanupJobKey);
		var existingTrigger = triggers.FirstOrDefault();
		var newTrigger = TriggerBuilder.Create().StartNow().WithCalendarIntervalSchedule(x =>
			x
			.WithIntervalInMinutes(5)
			.InTimeZone(GetTimeZone(appSettings.TimeZone))
			.PreserveHourOfDayAcrossDaylightSavings(true)
			.WithMisfireHandlingInstructionFireAndProceed()
		).StartAt(DateBuilder.TodayAt(0, 0, 0)).Build();
		if (existingTrigger != null)
		{
			await scheduler.RescheduleJob(existingTrigger.Key, newTrigger);
		}
		else
		{
			var barmanCleanupJob = JobBuilder.Create<BarmanCleanupJob>().WithIdentity(Constants.BarmanCleanupJobKey).Build();
			await scheduler.ScheduleJob(barmanCleanupJob, newTrigger);
		}
	}

	public (ArchiveAction Action, Instant? ExpirationDate) DetermineAction(Instant backupTime, string dbId)
	{
		if (!appSettings.Databases.TryGetValue(dbId, out var db))
		{
			return (ArchiveAction.None, null);
		}
		var now = clock.GetCurrentInstant().InZone(appSettings.TimeZone).LocalDateTime;
		var backupDate = backupTime.InZone(appSettings.TimeZone).LocalDateTime;
		foreach (var policy in db.ArchivalPolicies)
		{
			var policyExpirationDate = GetExpirationDate(policy.Retention, backupDate);
			if (MatchesPolicy(policy, backupDate))
			{
				if (policyExpirationDate > now)
				{
					return (ArchiveAction.Keep, policyExpirationDate.InZoneLeniently(appSettings.TimeZone).ToInstant());
				}
			}
		}
		return (ArchiveAction.None, null);
	}

	private static int GetDayOfWeekInstanceOfMonth(LocalDateTime date)
	{
		return date.Day switch
		{
			< 8 => 1,
			>= 8 and < 15 => 2,
			>= 15 and < 22 => 3,
			>= 22 and < 29 => 4,
			_ => 5
		};
	}

	private static int GetDayOfWeekInstanceOfYear(LocalDateTime date)
	{
		var firstDayOfYear = new LocalDateTime(date.Year, 1, 1, 0, 0);
		var daysUntilFirstOfYear = ((int)date.DayOfWeek - (int)firstDayOfYear.DayOfWeek + 7) % 7;

		var firstOfYear = firstDayOfYear.PlusDays(daysUntilFirstOfYear);

		// Calculate the number of weeks between the first day and the target date
		return (date - firstOfYear).Days / 7 + 1;
	}

	private static LocalDateTime GetExpirationDate(ArchiveRetentionPolicy retentionPolicy, LocalDateTime beginDate)
	{
		return retentionPolicy.Units switch
		{
			DurationUnits.Day => beginDate.PlusDays(retentionPolicy.Duration),
			DurationUnits.Week => beginDate.PlusWeeks(retentionPolicy.Duration),
			DurationUnits.Month => beginDate.PlusMonths(retentionPolicy.Duration),
			DurationUnits.Year => beginDate.PlusYears(retentionPolicy.Duration),
			_ => throw new NotImplementedException()
		};
	}

	private static bool MatchesPolicy(ArchivalPolicy policy, LocalDateTime backupDate)
	{
		switch (policy.Strategy.Type)
		{
			case RetentionStrategyType.DayOfWeek:
				if (policy.Strategy.DayOfWeek == backupDate.DayOfWeek)
				{
					return true;
				}
				break;

			case RetentionStrategyType.TargetDay:
				if (policy.Strategy.DayOfWeek == backupDate.DayOfWeek)
				{
					return policy.Strategy.OccurrenceUnits switch
					{
						DurationUnits.Month => policy.Strategy.Occurrence == GetDayOfWeekInstanceOfMonth(backupDate),
						DurationUnits.Year => policy.Strategy.Occurrence == GetDayOfWeekInstanceOfYear(backupDate),
						_ => throw new NotImplementedException(),
					};
				}
				break;
		}
		return false;
	}
}

public class RetentionStrategy
{
	public IsoDayOfWeek DayOfWeek { get; init; }
	public int Occurrence { get; init; }
	public DurationUnits OccurrenceUnits { get; init; }
	public RetentionStrategyType Type { get; init; }
}
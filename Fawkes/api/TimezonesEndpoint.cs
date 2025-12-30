using Fawkes.Api.Settings;
using NodaTime;
using Quartz;
using Voyager;

namespace Fawkes.Api;

[VoyagerEndpoint("/timeZones")]
public class TimezonesEndpoint
{
	public record TimeZoneDescription(string Id, string Name);

	public IResult Get()
	{
		return TypedResults.Ok(new
		{
			timeZones = DateTimeZoneProviders.Tzdb.Ids
		});
	}
}

[VoyagerEndpoint("settings/timeZone")]
public class TimezoneSettingsEndpoint
{
	public static IResult Get(AppSettings settings)
	{
		return TypedResults.Ok(new { settings.TimeZoneName });
	}

	public record PutTimezoneRequest(string TimeZone);

	public static async Task<IResult> Put(PutTimezoneRequest request, AppSettings settings, ISchedulerFactory schedulerFactory)
	{
		var foundTimeZone = DateTimeZoneProviders.Tzdb[request.TimeZone];
		settings.TimeZoneName = request.TimeZone;
		await settings.Save();
		await ArchiveService.UpdateCleanupJob(settings, await schedulerFactory.GetScheduler());
		return TypedResults.Ok();
	}
}
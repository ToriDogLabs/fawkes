using Quartz;

namespace Fawkes.Api;

public partial class BarmanCronJob : IJob
{
	public async Task Execute(IJobExecutionContext? context = null)
	{
		await Terminal.RunBarman("-q", "cron");
	}
}
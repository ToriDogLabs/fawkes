namespace fawkes_restore;

public static class Clean
{
	public static async Task<int> Start(string? dataDir)
	{
		var pgData = dataDir ?? Environment.GetEnvironmentVariable("PGDATA");
		if (pgData == null)
		{
			Console.WriteLine("Unable to determine pgdirectory");
			return 2;
		}

		var signalPath = Path.Join(pgData, "signal.recovery");
		if (File.Exists(pgData))
		{
			File.Delete(signalPath);
		}

		var confPath = Path.Join(pgData, "postgresql.conf");
		var lines = (await File.ReadAllLinesAsync(confPath)).ToList();
		for (var i = 0; i < lines.Count; ++i)
		{
			if (lines[i].StartsWith("restore_command") || lines[i].StartsWith("recovery_target_time"))
			{
				lines[i] = $"#{lines[i]}";
			}
		}

		await File.WriteAllLinesAsync(confPath, lines);
		return 0;
	}
}
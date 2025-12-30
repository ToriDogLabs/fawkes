using Medallion.Shell;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fawkes.Api;

public static class Terminal
{
	public static TerminalBuilder Barman()
	{
		return new TerminalBuilder().Name("barman").Executable("barman");
	}

	public static async Task<KeepStatus> GetArchivalStatus(S3BackupLocation config, string server, string backupId)
	{
		var result = await Run("keep status", "barman-cloud-backup-keep", "--endpoint-url", config.Endpoint,
			"-P", config.Id, "-s", config.S3Path, server, backupId);
		if (!Enum.TryParse<KeepStatus>(result.StandardOutput.Replace("Keep: ", "").Replace("\n", ""), out var status))
		{
			status = KeepStatus.nokeep;
		}
		return status;
	}

	public static async Task<KeepStatus> GetLocalStatus(string server, string backupId)
	{
		var result = await RunBarman("-f", "json", "keep", "--status", server, backupId);
		var output = JsonSerializer.Deserialize<Dictionary<string, KeepOutput>>(result.StandardOutput);
		if ((output?.TryGetValue(server, out var keepOutput) ?? false))
		{
			if (keepOutput.KeepStatus?.Message.HasValue ?? false)
			{
				return keepOutput.KeepStatus.Message.Value;
			}
		}
		return KeepStatus.nokeep;
	}

	public static Task<CommandResult> Run(string description, string executable, params string[] arguments)
	{
		return Run(description, executable, (IEnumerable<string>)arguments);
	}

	public static async Task<CommandResult> Run(string description, string executable, IEnumerable<string> arguments)
	{
		Console.WriteLine($"starting {description}");
		var command = Command.Run(executable, arguments);//.RedirectTo(Console.Out).RedirectStandardErrorTo(Console.Out);
		var result = await command.Task;
		Console.WriteLine($"finished {description}");
		return result;
	}

	public static Task<CommandResult> RunBarman(params string[] arguments)
	{
		return RunBarman((IEnumerable<string>)arguments);
	}

	public static Task<CommandResult> RunBarman(IEnumerable<string> arguments)
	{
		var description = $"barman {string.Join(' ', arguments)}";
		return Run(description, "barman", arguments);
	}
}

public class KeepOutput
{
	[JsonPropertyName("keep_status")]
	public Status? KeepStatus { get; set; }

	public class Status
	{
		[JsonPropertyName("description")]
		public string? Description { get; set; }

		[JsonPropertyName("message")]
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public KeepStatus? Message { get; set; }
	}
}

public class TerminalBuilder
{
	private string[] arguments = [];
	private string executable = string.Empty;
	private string? name = null;
	private TimeSpan? timeout = null;

	public TerminalBuilder Arguments(params string[] arguments)
	{
		this.arguments = arguments;
		return this;
	}

	public TerminalBuilder Executable(string executable)
	{
		this.executable = executable;
		return this;
	}

	public TerminalBuilder Name(string name)
	{
		this.name = name;
		return this;
	}

	public async Task<CommandResult> Run()
	{
		var description = $"{name ?? executable} {string.Join(' ', arguments)}";
		Console.WriteLine($"starting {description}");
		var command = Command.Run(executable, arguments, op =>
		{
			if (timeout.HasValue)
			{
				op.Timeout(timeout.Value);
			}
		});//.RedirectTo(Console.Out).RedirectStandardErrorTo(Console.Out);
		var result = await command.Task;
		Console.WriteLine($"finished {description}");
		return result;
	}

	public TerminalBuilder Timeout(TimeSpan timeout)
	{
		this.timeout = timeout;
		return this;
	}
}
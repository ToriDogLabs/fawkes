using fawkes_restore;
using System.CommandLine;

var rootCommand = new RootCommand();
var returnCode = 100;

var restoreCommand = new Command("restore", "Restore base backup");
var accessKeyOption = new Option<string>("--access_key") { IsRequired = true };
var secretKeyOption = new Option<string>("--secret_key") { IsRequired = true };
var pathOption = new Option<string>("--path") { IsRequired = true };
var backupIdOption = new Option<string>("--backupId") { IsRequired = true };
var endpointOption = new Option<string?>("--endpoint");
var recoverDirOption = new Option<string?>("--recover_dir");
var targetTimeOption = new Option<string?>("--target_time");

restoreCommand.AddOption(accessKeyOption);
restoreCommand.AddOption(secretKeyOption);
restoreCommand.AddOption(pathOption);
restoreCommand.AddOption(backupIdOption);
restoreCommand.AddOption(endpointOption);
restoreCommand.AddOption(recoverDirOption);
restoreCommand.AddOption(targetTimeOption);
restoreCommand.SetHandler(async (string accessKey, string secretKey, string path,
	string backupId, string? endpoint, string? recoverDir, string? targetTime) =>
	{
		returnCode = await RestoreService.Run(new()
		{
			AccessKey = accessKey,
			BackupId = backupId,
			Endpoint = endpoint,
			Path = path,
			RecoverDir = recoverDir,
			SecretKey = secretKey,
			TargetTime = targetTime
		});
	},
	accessKeyOption,
	secretKeyOption,
	pathOption,
	backupIdOption,
	endpointOption,
	recoverDirOption,
	targetTimeOption
);
rootCommand.AddCommand(restoreCommand);

var walCommand = new Command("restore_wal", "Restore wal file");
var walFileOption = new Option<string>(["--file", "-f"]) { IsRequired = true };
var walPathOption = new Option<string>(["--path", "-p"]) { IsRequired = true };
walCommand.AddOption(walFileOption);
walCommand.AddOption(walPathOption);

walCommand.SetHandler(async (string file, string path) =>
	{
		try
		{
			returnCode = (int)await RestoreWalService.Run(new() { File = file, Path = path });
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			returnCode = 99;
		}
	},
	walFileOption,
	walPathOption
);
rootCommand.AddCommand(walCommand);

var cleanCommand = new Command("clean");
var dataDirOption = new Option<string?>("--pg_data");
cleanCommand.AddOption(dataDirOption);

cleanCommand.SetHandler(async (string? dataDir) =>
{
	returnCode = await Clean.Start(dataDir);
}, dataDirOption);
rootCommand.AddCommand(cleanCommand);

await rootCommand.InvokeAsync(args);
return returnCode;
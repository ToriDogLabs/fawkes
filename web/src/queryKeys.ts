export const queryKeys = {
	retention: ["retention"],
	schedule: (dbId: string) => ["schedule", dbId],
	serverBackup: (server: string) => ["servers", server, "bacukps"],
	archivePolicy: ["archive_policy"],
	recoverCmd: (server: string, backupId: string, targetTime: string) => ["servers", server, "recover", backupId, targetTime],
	timezones: ["timezones"],
	savedTimezone: ["savedTimezone"],
	s3Config: ["s3Config"],
	backupLocations: ["backupLocations"],
	notifications: ["notifications"],
};

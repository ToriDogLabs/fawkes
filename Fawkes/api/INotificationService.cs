namespace Fawkes.Api;

public interface INotificationService
{
	Task AddBackupGroup(string dbId, BackupGroup backupGroup);

	Task ClearServerMessage(Guid id, string? message = null, MessageVariant variant = MessageVariant.Default);

	Task RemoveBackup(string dbId, string backupId, string locationId);

	Task<Guid> ServerMessage(Notification notification);

	List<Notification> GetActiveNotifications();
}
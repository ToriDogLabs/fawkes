using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Voyager;

namespace Fawkes.Api;

public enum MessageVariant
{
	Default,
	Destructive,
	Success,
	OnGoing
}

[VoyagerEndpoint("/notifications")]
public class NotificationEndpoint(INotificationService notifications)
{
	public List<Notification> Get()
	{
		return notifications.GetActiveNotifications();
	}
}

public class WebsocketNotificationService(IWebsocketClientProvider websocket) : INotificationService
{
	private readonly ConcurrentDictionary<Guid, Notification> activeNotifications = [];

	public List<Notification> GetActiveNotifications()
	{
		return activeNotifications.Values.ToList();
	}

	public async Task AddBackupGroup(string dbId, BackupGroup group)
	{
		await websocket.Get().SendAsync($"sync", new
		{
			action = "addBackupGroup",
			group,
			dbId
		});
	}

	public async Task ClearServerMessage(Guid id, string? message, MessageVariant variant = MessageVariant.Default)
	{
		await websocket.Get().SendAsync($"sync", new
		{
			id,
			action = "clearServerMessage",
			message,
			variant = Enum.GetName(variant)?.ToLower()
		});
		activeNotifications.TryRemove(id, out _);
	}

	public async Task RemoveBackup(string dbId, string backupId, string locationId)
	{
		await websocket.Get().SendAsync($"sync", new
		{
			action = "removeBackup",
			dbId,
			backupId,
			locationId
		});
	}

	public async Task<Guid> ServerMessage(Notification notification)
	{
		activeNotifications.TryAdd(notification.Id, notification);
		await websocket.Get().SendAsync($"sync", new
		{
			id = notification.Id,
			action = "serverMessage",
			notification,
		});
		return notification.Id;
	}
}

public class Notification(string message)
{
	public Guid Id { get; init; } = Guid.NewGuid();
	public string Message => message;
	public string? DbId { get; init; }
	public string? BackupLocationId { get; init; }
	public required MessageVariant Variant { get; init; }
}
using Microsoft.AspNetCore.SignalR;

namespace Fawkes.Api;

public interface IWebsocketClientProvider
{
	public IClientProxy Get();
}

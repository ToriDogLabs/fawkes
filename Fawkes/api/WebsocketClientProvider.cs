using Microsoft.AspNetCore.SignalR;

namespace Fawkes.Api;

public class WebsocketClientProvider : IWebsocketClientProvider
{
	private readonly IHubContext<SignalrHub> hub;

	public WebsocketClientProvider(IHubContext<SignalrHub> hub)
	{
		this.hub = hub;
	}

	public IClientProxy Get()
	{
		return hub.Clients.All;
	}
}

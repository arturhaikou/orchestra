using Microsoft.AspNetCore.SignalR;

namespace Orchestra.ApiService.Hubs;

public class NotificationHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace ECFD.Infrastructure.SignalR;

// Lives next to SignalRNotifier so the notifier can inject IHubContext<DashboardHub>.
// A plain IHubContext<Hub> has its own connection registry and never reaches dashboard clients.
public class DashboardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}

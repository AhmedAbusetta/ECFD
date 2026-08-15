using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;
using ECFD.Application.Risk;

namespace ECFD.Api.Hubs;

public class DashboardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Scrum.Api.Auth;

/// <summary>Cada conexión se agrega a un grupo por usuario para poder empujarle avisos sin buscar su ConnectionId.</summary>
[Authorize]
public class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(Context.UserIdentifier!));
        await base.OnConnectedAsync();
    }

    public static string GroupFor(string userId) => $"user:{userId}";
}

/// <summary>El claim "sub" (no remapeado, ver MapInboundClaims = false) identifica al usuario, igual que UserId().</summary>
public class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst("sub")?.Value;
}

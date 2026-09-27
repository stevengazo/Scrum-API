using Microsoft.AspNetCore.Mvc;
using Scrum.Api.Auth;
using Scrum.Api.Models;
using Scrum.Api.Services;

namespace Scrum.Api.Controllers;

public record NotificationDto(int Id, NotificationType Type, string Text, string Link, bool Read, DateTime CreatedAt);

/// <summary>Avisos del usuario autenticado. El resto llega en vivo por /hubs/notifications; esto es la carga inicial.</summary>
[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    /// <summary>Últimos 50, más reciente primero.</summary>
    [HttpGet]
    public async Task<List<NotificationDto>> List() => await notifications.ListAsync(User.UserId());

    /// <summary>Marca uno como leído.</summary>
    /// <response code="204">Marcado.</response>
    /// <response code="404">No existe, o no es del usuario autenticado.</response>
    [HttpPost("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id) => this.ToNoContent(await notifications.MarkReadAsync(User.UserId(), id));

    /// <summary>Marca todos como leídos.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await notifications.MarkAllReadAsync(User.UserId());
        return NoContent();
    }
}

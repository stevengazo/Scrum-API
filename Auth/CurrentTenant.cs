namespace Scrum.Api.Auth;

/// <summary>Tenant del request actual, leído del claim "tid" del JWT. Lo usa el query filter global de
/// ScrumDbContext, así que fuera de un request HTTP (ej. Database.Migrate() en el arranque) TenantId es null y el
/// filtro simplemente no encuentra nada, sin romper.</summary>
public interface ICurrentTenant
{
    int? TenantId { get; }
}

public class CurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    public int? TenantId
    {
        get
        {
            var raw = accessor.HttpContext?.User.FindFirst("tid")?.Value;
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}

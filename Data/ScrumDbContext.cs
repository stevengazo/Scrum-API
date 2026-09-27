using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Scrum.Api.Auth;
using Scrum.Api.Models;

namespace Scrum.Api.Data;

/// <summary>El query filter global de cada tabla con TenantId acota TODAS las consultas LINQ (incluidas las que
/// hace UserManager de Identity por debajo) al tenant del usuario autenticado. Fuera de un request HTTP (ej.
/// Database.Migrate() en el arranque) currentTenant.TenantId es null, así que el filtro no encuentra nada, sin
/// romper.</summary>
public class ScrumDbContext(DbContextOptions<ScrumDbContext> options, ICurrentTenant currentTenant) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<AcceptanceCriterion> Criteria => Set<AcceptanceCriterion>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<TodoItem> Todos => Set<TodoItem>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Tenant>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(120);
            e.Property(t => t.Slug).HasMaxLength(80);
            e.HasIndex(t => t.Slug).IsUnique();
        });

        // TenantId en NoAction en las 9 tablas: varias ya cascadean entre sí (Project→Sprint/Story/Page,
        // Story→Criteria/Comment, AppUser→Todo/Notification), y SQL Server no admite dos rutas de cascada hacia la
        // misma tabla desde una misma raíz. La integridad de datos la da el filtro global de abajo, no el borrado
        // en cadena de Tenant (que hoy no tiene ninguna pantalla que lo dispare).
        foreach (var t in new[]
        {
            typeof(AppUser), typeof(Project), typeof(Sprint), typeof(Story), typeof(AcceptanceCriterion),
            typeof(Page), typeof(TodoItem), typeof(Comment), typeof(Notification),
        })
        {
            b.Entity(t).Property("TenantId").IsRequired();
            b.Entity(t).HasOne(typeof(Tenant)).WithMany().HasForeignKey("TenantId").OnDelete(DeleteBehavior.NoAction);
            b.Entity(t).HasIndex("TenantId");
            b.Entity(t).HasQueryFilter(BuildTenantFilter(t));
        }

        b.Entity<Project>(e =>
        {
            // Única por tenant, no global: dos organizaciones pueden usar la misma clave sin chocar.
            e.HasIndex(p => new { p.TenantId, p.Key }).IsUnique();
            e.Property(p => p.Key).HasMaxLength(8);
            e.Property(p => p.Name).HasMaxLength(120);
        });

        b.Entity<Sprint>(e =>
        {
            e.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.Property(s => s.Status).HasConversion<string>();
        });

        b.Entity<Story>(e =>
        {
            e.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
            // SQL Server no admite varias rutas de cascada hacia una tabla: SprintService devuelve las historias al
            // backlog antes de borrar el sprint, y UsersController las desasigna antes de borrar al usuario.
            e.HasOne<Sprint>().WithMany().HasForeignKey(s => s.SprintId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<AppUser>().WithMany().HasForeignKey(s => s.AssigneeId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(s => new { s.ProjectId, s.Number }).IsUnique();
            e.Property(s => s.Type).HasConversion<string>();
            e.Property(s => s.Status).HasConversion<string>();
            e.Property(s => s.Priority).HasConversion<string>();
            e.HasMany(s => s.Criteria).WithOne().HasForeignKey(c => c.StoryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TodoItem>(e =>
        {
            e.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Story>().WithMany().HasForeignKey(t => t.StoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(t => t.UserId);
            e.Property(t => t.Text).HasMaxLength(300);
        });

        b.Entity<Page>(e =>
        {
            e.HasOne<Project>().WithMany().HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Cascade);
            // SQL Server no permite cascada en una autorreferencia: PageService borra el subárbol completo.
            e.HasOne<Page>().WithMany().HasForeignKey(p => p.ParentId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<AppUser>().WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.SetNull);
            // El valor por defecto cubre las filas que ya existían antes de que hubiera hojas.
            e.Property(p => p.Kind).HasConversion<string>().HasDefaultValue(PageKind.Page);
        });

        b.Entity<Comment>(e =>
        {
            e.HasOne<Story>().WithMany().HasForeignKey(c => c.StoryId).OnDelete(DeleteBehavior.Cascade);
            // Sin cascada: la cascada hacia esta tabla ya la tiene la FK de Story.
            e.HasOne<AppUser>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.NoAction);
            e.Property(c => c.Text).HasMaxLength(2000);
            e.HasIndex(c => c.StoryId);
        });

        b.Entity<Notification>(e =>
        {
            e.HasOne<AppUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(n => n.Type).HasConversion<string>();
            e.Property(n => n.Text).HasMaxLength(300);
            e.Property(n => n.Link).HasMaxLength(300);
            e.HasIndex(n => new { n.UserId, n.CreatedAt });
        });
    }

    /// <summary>El tenant del request actual. Ojo: el filtro de abajo debe leerlo a través de ESTA propiedad del
    /// propio ScrumDbContext, nunca capturando "currentTenant" directamente — el modelo (con sus filtros) se
    /// construye una sola vez y se cachea entre instancias, así que un objeto ajeno capturado con
    /// Expression.Constant queda fijo para siempre con el primer valor que vio. Una referencia a un miembro de
    /// "this" es justamente el caso que EF Core reconoce y revincula con la instancia real de cada consulta.</summary>
    public int? CurrentTenantId => currentTenant.TenantId;

    /// <summary>Construye "e => EF.Property&lt;int&gt;(e, "TenantId") == this.CurrentTenantId" para el tipo dado,
    /// porque HasQueryFilter necesita repetirse por cada una de las 9 entidades y no hay un tipo base común.</summary>
    private LambdaExpression BuildTenantFilter(Type entityType)
    {
        var e = Expression.Parameter(entityType, "e");
        var tenantId = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(int)], e, Expression.Constant("TenantId"));
        var currentTenantId = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
        var body = Expression.Equal(Expression.Convert(tenantId, typeof(int?)), currentTenantId);
        return Expression.Lambda(body, e);
    }
}

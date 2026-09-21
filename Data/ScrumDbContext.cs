using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Scrum.Api.Models;

namespace Scrum.Api.Data;

public class ScrumDbContext(DbContextOptions<ScrumDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<AcceptanceCriterion> Criteria => Set<AcceptanceCriterion>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<TodoItem> Todos => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Project>(e =>
        {
            e.HasIndex(p => p.Key).IsUnique();
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
            e.HasOne<Sprint>().WithMany().HasForeignKey(s => s.SprintId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<AppUser>().WithMany().HasForeignKey(s => s.AssigneeId).OnDelete(DeleteBehavior.SetNull);
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
            e.HasOne<Page>().WithMany().HasForeignKey(p => p.ParentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.SetNull);
            // El valor por defecto cubre las filas que ya existían antes de que hubiera hojas.
            e.Property(p => p.Kind).HasConversion<string>().HasDefaultValue(PageKind.Page);
        });
    }
}

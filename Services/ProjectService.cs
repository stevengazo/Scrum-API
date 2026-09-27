using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Scrum.Api.Controllers;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Services;

public partial class ProjectService(ScrumDbContext db) : IProjectService
{
    [GeneratedRegex("^[A-Z][A-Z0-9]{1,7}$")]
    private static partial Regex KeyPattern();

    /// <summary>Filtra sobre la entidad y proyecta al final: EF no traduce filtros sobre un DTO ya construido.</summary>
    private IQueryable<ProjectDto> Dtos(IQueryable<Project> source) => source.AsNoTracking().OrderBy(p => p.Name).Select(p => new ProjectDto(
        p.Id, p.Key, p.Name, p.Description, p.Color, p.Archived,
        db.Stories.Count(s => s.ProjectId == p.Id),
        db.Stories.Count(s => s.ProjectId == p.Id && s.Status == StoryStatus.Done)));

    public async Task<IReadOnlyList<ProjectDto>> ListAsync() => await Dtos(db.Projects).ToListAsync();

    public async Task<Result<ProjectDto>> GetAsync(int id) =>
        await Dtos(db.Projects.Where(p => p.Id == id)).FirstOrDefaultAsync() is { } p ? p : Error.NotFound();

    public async Task<Result<ProjectDto>> CreateAsync(ProjectCreateInput input, int tenantId)
    {
        var key = (input.Key ?? "").Trim().ToUpperInvariant();
        if (!KeyPattern().IsMatch(key)) return Error.Invalid("La clave debe tener 2-8 caracteres (A-Z, 0-9) y empezar con letra.");
        if (string.IsNullOrWhiteSpace(input.Name)) return Error.Invalid("El nombre es obligatorio.");
        if (await db.Projects.AnyAsync(p => p.Key == key)) return Error.Conflict("Ya existe un proyecto con esa clave.");

        var p = new Project { TenantId = tenantId, Key = key, Name = input.Name.Trim(), Description = input.Description, Color = input.Color ?? "#6366f1" };
        db.Projects.Add(p);
        await db.SaveChangesAsync();
        return await Dtos(db.Projects.Where(x => x.Id == p.Id)).FirstAsync();
    }

    public async Task<Result> UpdateAsync(int id, ProjectUpdateInput input)
    {
        var p = await db.Projects.FindAsync(id);
        if (p is null) return Error.NotFound();
        if (string.IsNullOrWhiteSpace(input.Name)) return Error.Invalid("El nombre es obligatorio.");
        p.Name = input.Name.Trim();
        p.Description = input.Description;
        p.Color = input.Color ?? p.Color;
        p.Archived = input.Archived;
        await db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id) =>
        await db.Projects.Where(p => p.Id == id).ExecuteDeleteAsync() == 0 ? Error.NotFound() : Result.Success();
}

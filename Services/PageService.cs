using Microsoft.EntityFrameworkCore;
using Scrum.Api.Auth;
using Scrum.Api.Controllers;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Services;

public class PageService(ScrumDbContext db, ICurrentTenant currentTenant) : IPageService
{
    private const int MaxContentLength = 5_000_000;
    private const double OrderGap = 1024;
    /// <summary>Libro nuevo con una hoja vacía (la cuadrícula es virtual: 10.000 filas × 702 columnas).</summary>
    private const string EmptySheet = """{"version":2,"active":0,"sheets":[{"id":"s1","name":"Hoja1","data":{"rows":10000,"cols":702,"cells":{}}}]}""";

    public async Task<IReadOnlyList<PageSummary>> ListAsync() =>
        await db.Pages.AsNoTracking().OrderBy(p => p.Order)
            .Select(p => new PageSummary(p.Id, p.ProjectId, p.ParentId, p.Title, p.Icon, p.Order, p.UpdatedAt, p.Kind)).ToListAsync();

    public async Task<Result<PageDto>> GetAsync(Guid id) =>
        await db.Pages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new PageDto(p.Id, p.ProjectId, p.ParentId, p.Title, p.Icon, p.Content, p.Order, p.UpdatedAt, p.Kind))
            .FirstOrDefaultAsync() is { } page ? page : Error.NotFound();

    public async Task<Result<PageDto>> CreateAsync(PageCreateInput input, string userId)
    {
        var kind = input.Kind ?? PageKind.Page;
        var projectId = input.ProjectId;
        if (input.ParentId is { } pid)
        {
            if (kind == PageKind.Sheet) return Error.Invalid("Una hoja de cálculo no puede tener página padre.");
            var parent = await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
            if (parent is null) return Error.Invalid("La página padre no existe.");
            if (parent.Kind == PageKind.Sheet) return Error.Invalid("Una hoja de cálculo no puede tener subpáginas.");
            projectId = parent.ProjectId; // los hijos heredan el espacio del padre
        }
        if (projectId is { } prj && !await db.Projects.AnyAsync(p => p.Id == prj)) return Error.Invalid("El proyecto no existe.");

        var max = await db.Pages.Where(p => p.ParentId == input.ParentId && p.ProjectId == projectId).MaxAsync(p => (double?)p.Order) ?? 0;
        var page = new Page
        {
            TenantId = currentTenant.TenantId!.Value,
            Title = input.Title?.Trim() ?? "", Icon = input.Icon, ParentId = input.ParentId, ProjectId = projectId,
            Order = max + OrderGap, CreatedById = userId, Kind = kind,
            Content = kind == PageKind.Sheet ? EmptySheet : null,
        };
        db.Pages.Add(page);
        await db.SaveChangesAsync();
        return new PageDto(page.Id, page.ProjectId, page.ParentId, page.Title, page.Icon, page.Content, page.Order, page.UpdatedAt, page.Kind);
    }

    public async Task<Result> UpdateAsync(Guid id, PageUpdateInput input)
    {
        if (input.Content is { Length: > MaxContentLength }) return Error.Invalid("La página es demasiado grande.");
        var page = await db.Pages.FindAsync(id);
        if (page is null) return Error.NotFound();
        if (page.Kind == PageKind.Sheet && !IsSheetJson(input.Content)) return Error.Invalid("El contenido de la hoja no es válido.");
        page.Title = input.Title.Trim();
        page.Icon = input.Icon;
        page.Content = input.Content;
        page.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> MoveAsync(Guid id, PageMoveInput input)
    {
        var page = await db.Pages.FindAsync(id);
        if (page is null) return Error.NotFound();
        if (page.Kind == PageKind.Sheet && input.ParentId is not null) return Error.Invalid("Una hoja de cálculo no puede tener página padre.");

        // Impide ciclos: el nuevo padre no puede ser la propia página ni un descendiente suyo.
        for (var cursor = input.ParentId; cursor is { } c;)
        {
            if (c == id) return Error.Invalid("Una página no puede moverse dentro de sí misma.");
            cursor = await db.Pages.Where(p => p.Id == c).Select(p => p.ParentId).FirstOrDefaultAsync();
        }
        if (input.ParentId is { } np)
        {
            var parent = await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == np);
            if (parent is null) return Error.Invalid("La página padre no existe.");
            if (parent.Kind == PageKind.Sheet) return Error.Invalid("Una hoja de cálculo no puede tener subpáginas.");
            if (parent.ProjectId != page.ProjectId) await SetProjectRecursive(page, parent.ProjectId);
        }
        page.ParentId = input.ParentId;
        page.Order = input.Order;
        await db.SaveChangesAsync();
        return Result.Success();
    }

    /// <summary>Borra la página y todos sus descendientes (la FK del padre no cascadea en SQL Server).</summary>
    public async Task<Result> DeleteAsync(Guid id)
    {
        var projectId = await db.Pages.Where(p => p.Id == id).Select(p => (int?)p.ProjectId).FirstOrDefaultAsync();
        if (projectId is null) return Error.NotFound();

        var parents = (await db.Pages.Where(p => p.ProjectId == projectId && p.ParentId != null)
            .Select(p => new { p.Id, ParentId = p.ParentId!.Value }).ToListAsync()).ToLookup(p => p.ParentId, p => p.Id);
        var ids = new List<Guid> { id };
        for (var i = 0; i < ids.Count; i++) ids.AddRange(parents[ids[i]]);

        await db.Pages.Where(p => ids.Contains(p.Id)).ExecuteDeleteAsync();
        return Result.Success();
    }

    /// <summary>
    /// El contenido de una hoja es un libro { sheets: [{ name, data: { cells } }] } o, en el formato anterior, una sola hoja
    /// { rows, cols, cells }. Se rechaza cualquier otra cosa.
    /// </summary>
    private static bool IsSheetJson(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return false;

            if (root.TryGetProperty("sheets", out var sheets))
            {
                return sheets.ValueKind == System.Text.Json.JsonValueKind.Array
                    && sheets.GetArrayLength() is > 0 and <= 100
                    && sheets.EnumerateArray().All(s =>
                        s.ValueKind == System.Text.Json.JsonValueKind.Object
                        && s.TryGetProperty("name", out var name) && name.ValueKind == System.Text.Json.JsonValueKind.String
                        && s.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Object
                        && data.TryGetProperty("cells", out var cells) && cells.ValueKind == System.Text.Json.JsonValueKind.Object);
            }
            return root.TryGetProperty("rows", out var rows) && rows.ValueKind == System.Text.Json.JsonValueKind.Number
                && root.TryGetProperty("cols", out var cols) && cols.ValueKind == System.Text.Json.JsonValueKind.Number
                && root.TryGetProperty("cells", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Object;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private async Task SetProjectRecursive(Page page, int? projectId)
    {
        page.ProjectId = projectId;
        var children = await db.Pages.Where(p => p.ParentId == page.Id).ToListAsync();
        foreach (var child in children) await SetProjectRecursive(child, projectId);
    }
}

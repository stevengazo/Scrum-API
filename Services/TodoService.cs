using Microsoft.EntityFrameworkCore;
using Scrum.Api.Auth;
using Scrum.Api.Controllers;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Services;

/// <summary>Todas las operaciones se acotan al usuario dueño: nadie ve ni toca los items de otro.</summary>
public class TodoService(ScrumDbContext db, ICurrentTenant currentTenant) : ITodoService
{
    private const double OrderGap = 1024;
    private const int MaxTextLength = 300;

    public async Task<List<TodoDto>> ListAsync(string userId)
    {
        var items = await db.Todos.AsNoTracking().Where(t => t.UserId == userId)
            .OrderBy(t => t.Done).ThenBy(t => t.Order).ThenBy(t => t.Id).ToListAsync();
        return await ToDtos(items);
    }

    public async Task<Result<TodoDto>> CreateAsync(string userId, TodoCreateInput input)
    {
        var text = input.Text?.Trim() ?? "";

        if (input.StoryId is { } sid)
        {
            var title = await db.Stories.AsNoTracking().Where(s => s.Id == sid).Select(s => s.Title).FirstOrDefaultAsync();
            if (title is null) return Error.Invalid("La historia no existe.");
            if (text.Length == 0) text = title; // una historia agregada sin texto usa su título
        }
        if (text.Length == 0) return Error.Invalid("El texto es obligatorio.");
        if (text.Length > MaxTextLength) return Error.Invalid($"El texto no puede superar {MaxTextLength} caracteres.");

        var max = await db.Todos.Where(t => t.UserId == userId && !t.Done).MaxAsync(t => (double?)t.Order) ?? 0;
        var item = new TodoItem { TenantId = currentTenant.TenantId!.Value, UserId = userId, Text = text, StoryId = input.StoryId, Order = max + OrderGap };
        db.Todos.Add(item);
        await db.SaveChangesAsync();
        return (await ToDtos([item]))[0];
    }

    public async Task<Result> UpdateAsync(string userId, int id, TodoUpdateInput input)
    {
        var item = await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (item is null) return Error.NotFound();
        var text = input.Text?.Trim() ?? "";
        if (text.Length == 0) return Error.Invalid("El texto es obligatorio.");
        if (text.Length > MaxTextLength) return Error.Invalid($"El texto no puede superar {MaxTextLength} caracteres.");

        item.Text = text;
        item.CompletedAt = input.Done ? item.CompletedAt ?? DateTime.UtcNow : null;
        item.Done = input.Done;
        await db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<TodoDto>> AddPomodoroAsync(string userId, int id)
    {
        var item = await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (item is null) return Error.NotFound();
        item.Pomodoros++;
        await db.SaveChangesAsync();
        return (await ToDtos([item]))[0];
    }

    public async Task<Result> DeleteAsync(string userId, int id) =>
        await db.Todos.Where(t => t.Id == id && t.UserId == userId).ExecuteDeleteAsync() == 0 ? Error.NotFound() : Result.Success();

    public Task<int> ClearDoneAsync(string userId) => db.Todos.Where(t => t.UserId == userId && t.Done).ExecuteDeleteAsync();

    /// <summary>Adjunta clave y título de la historia enlazada, para pintar el item sin pedir cada historia.</summary>
    private async Task<List<TodoDto>> ToDtos(List<TodoItem> items)
    {
        var ids = items.Where(i => i.StoryId is not null).Select(i => i.StoryId!.Value).Distinct().ToList();
        var stories = ids.Count == 0
            ? []
            : await (from s in db.Stories.AsNoTracking()
                     join p in db.Projects.AsNoTracking() on s.ProjectId equals p.Id
                     where ids.Contains(s.Id)
                     select new { s.Id, s.ProjectId, s.Number, p.Key, s.Title }).ToDictionaryAsync(x => x.Id);

        return items.Select(i =>
        {
            var s = i.StoryId is { } id && stories.TryGetValue(id, out var found) ? found : null;
            return new TodoDto(i.Id, i.Text, i.Done, i.StoryId, s is null ? null : $"{s.Key}-{s.Number}", s?.Title, s?.ProjectId,
                i.Pomodoros, i.Order, i.CreatedAt, i.CompletedAt);
        }).ToList();
    }
}

using Microsoft.AspNetCore.Identity;

namespace Scrum.Api.Models;

public enum StoryStatus { Todo, InProgress, Review, Done }
public enum StoryType { Story, Bug, Task, Spike }
public enum Priority { Low, Medium, High, Critical }
public enum SprintStatus { Planned, Active, Completed }
/// <summary>Página = documento (Lexical). Sheet = hoja de cálculo independiente (su Content es un SheetData JSON).</summary>
public enum PageKind { Page, Sheet }

public static class Roles
{
    public const string Admin = "Admin";
    public const string ScrumMaster = "ScrumMaster";
    public const string ProductOwner = "ProductOwner";
    public const string Developer = "Developer";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, ScrumMaster, ProductOwner, Developer, Viewer];
}

public static class Policies
{
    /// <summary>Crear/editar proyectos.</summary>
    public const string ManageProjects = nameof(ManageProjects);
    /// <summary>Crear/editar/iniciar/cerrar sprints.</summary>
    public const string ManageSprints = nameof(ManageSprints);
    /// <summary>Cualquier escritura de contenido (historias, páginas). Excluye a Viewer.</summary>
    public const string Contribute = nameof(Contribute);
    public const string ManageUsers = nameof(ManageUsers);
}

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public string Color { get; set; } = "#64748b";
}

public class Project
{
    public int Id { get; set; }
    /// <summary>Prefijo de las claves de historia: AGL → AGL-12.</summary>
    public required string Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string Color { get; set; } = "#6366f1";
    public bool Archived { get; set; }
    /// <summary>Contador que nunca se reutiliza, aunque se borren historias.</summary>
    public int NextStoryNumber { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Sprint
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public required string Name { get; set; }
    public string? Goal { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public SprintStatus Status { get; set; } = SprintStatus.Planned;
    /// <summary>Puntos que el equipo se compromete a completar.</summary>
    public int Capacity { get; set; }
}

public class Story
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int Number { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public StoryType Type { get; set; } = StoryType.Story;
    public StoryStatus Status { get; set; } = StoryStatus.Todo;
    public Priority Priority { get; set; } = Priority.Medium;
    public int? Points { get; set; }
    /// <summary>Null = vive en el product backlog.</summary>
    public int? SprintId { get; set; }
    public string? AssigneeId { get; set; }
    /// <summary>Posición dentro de su columna o del backlog. Menor = más arriba.</summary>
    public double Order { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<AcceptanceCriterion> Criteria { get; set; } = [];
}

public class AcceptanceCriterion
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public required string Text { get; set; }
    public bool Done { get; set; }
    public int Order { get; set; }
}

/// <summary>Página estilo Notion. El contenido es el estado serializado de Lexical.</summary>
public class Page
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? ProjectId { get; set; }
    public Guid? ParentId { get; set; }
    public PageKind Kind { get; set; } = PageKind.Page;
    public string Title { get; set; } = "";
    public string? Icon { get; set; }
    public string? Content { get; set; }
    public double Order { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Nota personal de la lista "To-do" de un usuario, opcionalmente ligada a una historia.</summary>
public class TodoItem
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string Text { get; set; }
    public bool Done { get; set; }
    /// <summary>Historia de la que nació el item. Null = nota libre.</summary>
    public int? StoryId { get; set; }
    /// <summary>Pomodoros completados sobre este item.</summary>
    public int Pomodoros { get; set; }
    public double Order { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Scrum.Api.Auth;
using Scalar.AspNetCore;
using Scrum.Api.Data;
using Scrum.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecurityTransformer>());
builder.Services.AddDbContext<ScrumDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")
        ?? "Server=localhost,1433;Database=Scrum;User Id=sa;Password=Scrum_Dev_Passw0rd;TrustServerCertificate=True"));
builder.Services.AddScrumAuth(builder.Configuration, builder.Environment);
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ISprintService, SprintService>();
builder.Services.AddScoped<IStoryService, StoryService>();
builder.Services.AddScoped<IPageService, PageService>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IMetricsService, MetricsService>();

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
// AllowCredentials: el hub de notificaciones negocia con cookies/headers de sesión del navegador; es compatible con
// WithOrigins (a diferencia de AllowAnyOrigin).
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ScrumDbContext>().Database.Migrate();
}
await app.Services.SeedRolesAsync();

if (app.Environment.IsDevelopment())
{
    // Especificación OpenAPI en /openapi/v1.json y documentación interactiva en /scalar.
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(o => o.WithTitle("Scrum API")).AllowAnonymous();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<Scrum.Api.Auth.NotificationsHub>("/hubs/notifications");
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

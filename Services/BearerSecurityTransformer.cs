using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Scrum.Api.Services;

/// <summary>Declara el esquema JWT Bearer para que la documentación permita autenticarse y probar los endpoints.</summary>
public class BearerSecurityTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info = new OpenApiInfo { Title = "Scrum API", Version = "v1", Description = "Proyectos, sprints, historias y páginas de documentación." };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" },
        };
        return Task.CompletedTask;
    }
}

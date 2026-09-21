# Scrum-API

.NET 10 Web API (controladores) + EF Core con SQLite + Identity/JWT. Proyectos → Sprints → Historias, más páginas de documentación.

## Comandos

```
dotnet run --urls http://localhost:8080        # API
dotnet build                                   # debe compilar sin errores
dotnet ef migrations add <Nombre> -o Data/Migrations   # (tool local en dotnet-tools.json)
```

La base se migra sola al arrancar (`Database.Migrate()`), y los roles se siembran.

## Documentación de la API

En Development:
- Especificación OpenAPI: `/openapi/v1.json`
- Documentación interactiva (Scalar): `/scalar` — permite autenticarse con el JWT (esquema Bearer).

Se genera a partir del código: cada acción de los controladores lleva `/// <summary>`, `<response>` y
`[ProducesResponseType]`; los DTO documentan campos con `<param>`. El proyecto tiene `GenerateDocumentationFile`.
**Al añadir o cambiar un endpoint, documentarlo así en el mismo cambio.**

## Arquitectura y SOLID

```
Controllers/   Capa HTTP: enrutado, autorización ([Authorize(Policies.X)]), DTO de entrada/salida. Sin lógica de negocio ni EF.
Services/      Lógica de negocio tras interfaces (IProjectService, ISprintService, IStoryService, IPageService…).
Data/          ScrumDbContext y migraciones.
Models/        Entidades, enums, roles y políticas.
Auth/          JWT, Identity, políticas de autorización.
```

- **S**: el controlador traduce HTTP; el servicio decide reglas; el `DbContext` persiste.
- **O**: nueva regla = cambio en el servicio; nuevo recurso = nueva pareja interfaz/servicio + controlador.
- **L/I**: interfaces pequeñas por agregado; los controladores dependen de la interfaz, no de la clase.
- **D**: los servicios se registran en `Program.cs` (`AddScoped<IX, X>`); los controladores nunca crean dependencias.

### Result pattern (`Services/Result.cs`)

Los servicios no conocen HTTP: devuelven `Result` / `Result<T>` con un `Error` (`NotFound`, `Invalid`, `Conflict`).
El controlador lo traduce con `this.ToOk(...)`, `this.ToNoContent(...)` o `this.ToFailure(...)` (404/400/409).
Los mensajes de error son texto plano (el cliente los muestra tal cual). Para un `Result<T>` cuyo `T` es una
interfaz no hay conversión implícita: usar `List<T>` o una clase concreta.

## Convenciones

- Mensajes de validación y textos de negocio en español.
- Autorización por políticas (`Policies.*`); el fallback exige usuario autenticado. Endpoints públicos: `[AllowAnonymous]`.
- Consultas de solo lectura con `AsNoTracking()`. Filtrar sobre la entidad y proyectar al DTO al final.
- `UsersController`/`AuthController` usan `UserManager` (ya es una abstracción); no envolverlos salvo que crezca su lógica.

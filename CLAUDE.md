# Scrum-API

.NET 10 Web API (controladores) + EF Core con SQL Server + Identity/JWT. **Multi-tenant**: cada organización
(`Tenant`) tiene sus datos aislados. Proyectos → Sprints → Historias, más páginas de documentación, comentarios,
métricas y notificaciones en tiempo real (SignalR).

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
Services/      Lógica de negocio tras interfaces (IProjectService, ISprintService, IStoryService, IPageService,
               ICommentService, INotificationService, IMetricsService…).
Data/          ScrumDbContext (con el query filter global multi-tenant) y migraciones.
Models/        Entidades, enums, roles y políticas.
Auth/          JWT, Identity, políticas de autorización, ICurrentTenant y el hub de SignalR.
```

## Multi-tenant

Cada fila de las tablas de negocio (`Project`, `Sprint`, `Story`, `AcceptanceCriterion`, `Page`, `TodoItem`,
`Comment`, `Notification`, `AppUser`) tiene `TenantId`. `ScrumDbContext` aplica un `HasQueryFilter` global por
cada una (ver `BuildTenantFilter` en `Data/ScrumDbContext.cs`), así que **toda consulta LINQ existente queda
acotada al tenant automáticamente** — no hace falta (ni conviene) filtrar por `TenantId` a mano en los servicios.

Al **crear** una fila sí hay que setear `TenantId` explícitamente (el filtro no lo hace por vos): tomarlo de
`User.TenantId()` en el controlador, o heredarlo de la entidad padre ya cargada (ej. un `Comment` toma el de su
`Story`).

**Regla de oro del filtro:** en `BuildTenantFilter`, el valor del tenant se lee a través de una propiedad del
propio `ScrumDbContext` (`CurrentTenantId`), nunca capturando `ICurrentTenant` directamente con
`Expression.Constant`. El modelo (con sus filtros) se cachea una sola vez entre instancias del contexto; capturar
un objeto ajeno lo deja fijo con el primer valor que vio. Una referencia a un miembro de `this` es el caso que EF
Core reconoce y revincula con la instancia real de cada consulta — cualquier otra forma rompe el aislamiento entre
organizaciones de forma sutil (funciona en el primer request, falla o mezcla datos después).

Rutas anónimas que necesitan buscar cruzando tenants (login por email, unicidad de email al registrar) usan
`.IgnoreQueryFilters()` explícitamente — son las únicas excepciones, y están comentadas en `AccountService.cs`
(compartida entre `AuthController` y `Mcp/Tools/AuthTools.cs`, ver más abajo).

## MCP (`Mcp/`)

El endpoint `/mcp` (SDK oficial `ModelContextProtocol.AspNetCore`) expone casi todo el sistema como tools, en el
mismo proceso y puerto que la API REST — sin repetir lógica de negocio, las tools llaman a los mismos
`Services/*`. Alcance: todo el CRUD **excepto los endpoints `DELETE`** (borrar queda afuera a propósito).

- **Auth por sesión, no por cuenta fija**: cada conexión MCP hace `login` (o `register_organization`) con su
  propio email/contraseña; la identidad queda en `McpSessionStore` (singleton, `ConcurrentDictionary<sessionId,
  McpSession>`), nunca una cuenta compartida.
- **`SessionMode = StatefulForInitializeClients`** en `Program.cs` es obligatorio: el default del SDK es sin
  estado (protocolo 2026-07-28+), lo que rompe el login-por-sesión porque cada tool call llegaría con un
  `McpServer.SessionId` distinto (o null). Este modo da sesión real (`Mcp-Session-Id`) a cualquier cliente que
  negocie el handshake `initialize` clásico — Claude Code, Claude Desktop y prácticamente todo cliente MCP actual.
- **`PrincipalScope.Require`** es la primera línea de casi toda tool: busca la `McpSession` de esa conexión y
  arma un `ClaimsPrincipal` con los mismos claims que `TokenService.Create` (`sub`, `tid`, `role`×N), asignado al
  `HttpContext.User` ambiente. A partir de ahí, `User.TenantId()`/`UserId()`, `ICurrentTenant` (y por lo tanto el
  filtro global de tenant) y las políticas de autorización funcionan exactamente igual que en un request REST —
  nada de esto se reimplementó para MCP.
- **`PolicyGuard.RequireAsync`** reemplaza a `[Authorize(Policies.X)]`: como las tools no pasan por el pipeline
  de MVC, cada tool que escribe llama esto explícitamente con la misma política que usaría su controlador
  equivalente (`IAuthorizationService.AuthorizeAsync`, no una reimplementación de roles).
- **Un archivo por recurso** en `Mcp/Tools/` (`[McpServerToolType]`), mismo espíritu que un archivo por recurso
  en `Controllers/`. Los métodos usan los DTO/record que ya existen en `Controllers/*.cs`.
- Al agregar un endpoint REST nuevo que no sea `DELETE`, agregar también su tool MCP en el archivo de recurso
  correspondiente (mismo patrón: `PrincipalScope.Require` → `PolicyGuard` si escribe → llamar al `Service` → tirar
  `McpException` si `Result.Error`).

Probar el endpoint a mano: `npx @modelcontextprotocol/inspector --cli http://localhost:8080/mcp --method
tools/list`, o conectarlo de verdad con `claude mcp add --transport http scrum http://<host>:7001/mcp`.

## Notificaciones en tiempo real

`Auth/NotificationsHub.cs` expone `/hubs/notifications` (SignalR). Cada conexión se agrega a un grupo por usuario
(`user:{id}`, vía `IUserIdProvider` sobre el claim `"sub"`). El JWT llega por querystring (`?access_token=`) porque
el WebSocket no puede mandar el header `Authorization` (ver `OnMessageReceived` en `Auth/Auth.cs`).
`INotificationService.NotifyAsync` guarda el aviso y lo empuja por el hub en el mismo paso; los servicios que
disparan notificaciones (`StoryService`, `SprintService`, `CommentService`) la inyectan como cualquier otra
dependencia.

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

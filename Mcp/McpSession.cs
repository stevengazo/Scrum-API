namespace Scrum.Api.Mcp;

/// <summary>Identidad de quien inició sesión desde una conexión MCP (tool `login`). Vive en memoria, atada al
/// SessionId que el SDK asigna a esa conexión — nunca se persiste ni cruza sesiones.</summary>
public record McpSession(string UserId, int TenantId, string[] Roles, string DisplayName, string Email);

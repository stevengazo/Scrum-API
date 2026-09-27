using System.Collections.Concurrent;

namespace Scrum.Api.Mcp;

/// <summary>Mapa SessionId (MCP) → McpSession, en memoria. Singleton porque una sesión MCP dura mientras dura la
/// conexión, no un request; si el proceso se reinicia hay que volver a hacer login (no hay nada que perder: no
/// guarda contraseñas ni JWT, solo la identidad ya validada).</summary>
public class McpSessionStore
{
    private readonly ConcurrentDictionary<string, McpSession> _sessions = new();

    public void Set(string mcpSessionId, McpSession session) => _sessions[mcpSessionId] = session;

    public McpSession? Get(string mcpSessionId) => _sessions.GetValueOrDefault(mcpSessionId);

    public void Clear(string mcpSessionId) => _sessions.TryRemove(mcpSessionId, out _);
}

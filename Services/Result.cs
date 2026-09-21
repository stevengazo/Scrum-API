using Microsoft.AspNetCore.Mvc;

namespace Scrum.Api.Services;

public enum ErrorKind { NotFound, Invalid, Conflict }

/// <summary>Fallo de negocio, independiente de HTTP. El controlador lo traduce a un código de estado.</summary>
public record Error(ErrorKind Kind, string Message)
{
    public static Error NotFound() => new(ErrorKind.NotFound, "");
    public static Error Invalid(string message) => new(ErrorKind.Invalid, message);
    public static Error Conflict(string message) => new(ErrorKind.Conflict, message);
}

/// <summary>Resultado de una operación sin valor de retorno.</summary>
public class Result
{
    public Error? Error { get; init; }
    public bool Ok => Error is null;

    public static Result Success() => new();
    public static implicit operator Result(Error error) => new() { Error = error };
}

/// <summary>Resultado de una operación que devuelve un valor. Se construye implícitamente desde el valor o el error.</summary>
public class Result<T> : Result
{
    public T? Value { get; init; }

    public static implicit operator Result<T>(T value) => new() { Value = value };
    public static implicit operator Result<T>(Error error) => new() { Error = error };
}

public static class ResultExtensions
{
    /// <summary>Traduce un fallo a su respuesta HTTP: 404, 400 o 409.</summary>
    public static ActionResult ToFailure(this ControllerBase c, Error e) => e.Kind switch
    {
        ErrorKind.NotFound => c.NotFound(),
        ErrorKind.Conflict => c.Conflict(e.Message),
        _ => c.BadRequest(e.Message),
    };

    /// <summary>204 si salió bien, o el fallo correspondiente.</summary>
    public static IActionResult ToNoContent(this ControllerBase c, Result r) => r.Error is { } e ? c.ToFailure(e) : c.NoContent();

    /// <summary>200 con el valor, o el fallo correspondiente.</summary>
    public static ActionResult<T> ToOk<T>(this ControllerBase c, Result<T> r) => r.Error is { } e ? c.ToFailure(e) : r.Value!;
}

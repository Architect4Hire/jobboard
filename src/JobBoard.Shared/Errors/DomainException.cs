using Microsoft.AspNetCore.Http;

namespace JobBoard.Shared.Errors;

/// <summary>
/// Thrown by a service's business layer when a domain rule is violated (e.g. applying to a closed job).
/// Carries a machine-readable <see cref="Code"/> and the 4xx <see cref="StatusCode"/> the
/// <see cref="GlobalExceptionHandler"/> maps it to. Throw directly or subclass for a specific rule.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message, int statusCode = StatusCodes.Status409Conflict)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    /// <summary>Stable, machine-readable identifier for the rule that was violated.</summary>
    public string Code { get; }

    /// <summary>The 4xx status the failure maps to (defaults to 409 Conflict).</summary>
    public int StatusCode { get; }
}

using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JobBoard.Shared.Errors;

/// <summary>
/// The single place service errors become HTTP responses. FluentValidation failures map to 400 with
/// field-level detail; <see cref="DomainException"/> maps to its own 4xx; anything else is left to the
/// framework as a 500 (so nothing internal leaks). Registered via <c>AddSharedExceptionHandler()</c> and
/// activated by <c>app.UseExceptionHandler()</c> in each host.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, response) = exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                new ErrorResponse(
                    "validation",
                    "One or more validation errors occurred.",
                    validation.Errors
                        .Select(e => new ErrorDetail(e.PropertyName, e.ErrorMessage))
                        .ToArray())),
            DomainException domain => (
                domain.StatusCode,
                new ErrorResponse(domain.Code, domain.Message)),
            _ => (0, (ErrorResponse?)null),
        };

        if (response is null)
        {
            // Not an error we translate — let the framework produce a 500 without exposing details.
            return false;
        }

        _logger.LogWarning(exception, "Handled {ExceptionType} as {StatusCode}", exception.GetType().Name, statusCode);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}

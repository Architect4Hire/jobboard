namespace JobBoard.Shared.Errors;

/// <summary>
/// The single error shape every service returns, written by <see cref="GlobalExceptionHandler"/>. A
/// machine-readable <paramref name="Code"/>, a human <paramref name="Message"/>, and optional field-level
/// <paramref name="Errors"/> (populated for validation failures).
/// </summary>
public sealed record ErrorResponse(string Code, string Message, IReadOnlyList<ErrorDetail>? Errors = null);

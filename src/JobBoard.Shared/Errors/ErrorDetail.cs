namespace JobBoard.Shared.Errors;

/// <summary>One field-level problem within an <see cref="ErrorResponse"/> (e.g. a validation failure).</summary>
public sealed record ErrorDetail(string Field, string Message);

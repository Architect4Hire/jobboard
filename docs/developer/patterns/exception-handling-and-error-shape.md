# Exception Handling & Error Shape

*One `IExceptionHandler`, registered once per host, turns two known exception types into one JSON
error shape. Everything else becomes a 500 with no leaked detail — by construction, not by discipline.*

## The problem this solves

Without a single translation point, every controller ends up with its own `try/catch` (or worse, none),
and every service invents its own error JSON shape — which the Angular app then has to special-case per
endpoint. Centralizing the exception→HTTP mapping in `JobBoard.Shared` means a business layer only ever
has to *throw*, never format a response, and the frontend can parse one shape regardless of which
service answered.

## How it works here

[`GlobalExceptionHandler`](../../../src/JobBoard.Shared/Errors/GlobalExceptionHandler.cs) implements
ASP.NET Core's `IExceptionHandler` and recognizes exactly two exception types:

```csharp
var (statusCode, response) = exception switch
{
    ValidationException validation => (
        StatusCodes.Status400BadRequest,
        new ErrorResponse("validation", "One or more validation errors occurred.",
            validation.Errors.Select(e => new ErrorDetail(e.PropertyName, e.ErrorMessage)).ToArray())),
    DomainException domain => (domain.StatusCode, new ErrorResponse(domain.Code, domain.Message)),
    _ => (0, (ErrorResponse?)null),
};

if (response is null) return false;   // not ours — let the framework produce a bare 500
```

`ValidationException` is FluentValidation's own type, thrown by a facade's
`ValidateAndThrowAsync` call (see [Layered Service Architecture](./layered-service-architecture.md)) —
its field-level errors flow straight into `ErrorResponse.Errors`.
[`DomainException`](../../../src/JobBoard.Shared/Errors/DomainException.cs) is what a business layer
throws for a data-dependent rule violation:

```csharp
public class DomainException : Exception
{
    public DomainException(string code, string message, int statusCode = StatusCodes.Status409Conflict)
        : base(message) { Code = code; StatusCode = statusCode; }
    public string Code { get; }
    public int StatusCode { get; }
}
```

— e.g. `throw new DomainException("job.not_open", "...", StatusCodes.Status404NotFound)` in
[`JobBusiness.CloseAsync`](../../../src/JobBoard.Jobs.Core/Business/JobBusiness.cs). The default status
(409 Conflict) fits the handler's most common caller: a concurrency conflict from
[Concurrency Control](./concurrency-control.md). Anything that *isn't* one of these two types returns
`false` from `TryHandleAsync`, which the framework turns into a generic 500 — deliberately, so an
unexpected exception never leaks its message or stack trace to a client.

Both the shape and the field-level detail type are plain records:

```csharp
public sealed record ErrorResponse(string Code, string Message, IReadOnlyList<ErrorDetail>? Errors = null);
public sealed record ErrorDetail(string Field, string Message);
```

Every service returns this exact shape, whether the failure was a validation error, a `DomainException`,
or (had it fallen through) the framework's own problem-details 500 — one contract the Angular app can
parse regardless of which service answered.

### Wiring

A host registers the handler and activates it in two calls, in
[`SharedServiceCollectionExtensions.cs`](../../../src/JobBoard.Shared/DependencyInjection/SharedServiceCollectionExtensions.cs)
and each host's `Program.cs`:

```csharp
builder.Services.AddSharedExceptionHandler();   // registers GlobalExceptionHandler + AddProblemDetails()
// ...
app.UseExceptionHandler();                      // activates it in the pipeline
```

No service writes its own exception middleware — this is the one place it happens, reused unchanged.

## Why

No dedicated ADR — this is a direct consequence of keeping validation and domain rules in `.Core` (per
[ADR-0005](../../adr/0005-thin-host-core-layered-library.md)) while still needing exactly one place that
turns a thrown exception into an HTTP response.

## Pitfalls / rules to respect

- **Throw `DomainException` for domain rule violations, not a raw exception.** Give it a stable,
  machine-readable `Code` — that's what a client (or a support engineer reading a log) keys off, not the
  message text.
- **Validation stays in FluentValidation, at the facade.** Don't hand-roll a 400 response anywhere; let
  `ValidateAndThrowAsync` + the global handler produce it.
- **Never make an unrecognized exception type leak detail.** If you need a new *category* of client-safe
  error, give it its own subclass of `DomainException` (or extend the handler's switch) rather than
  reshaping what a generic exception returns.
- **The error shape is the same across every service.** Don't invent a per-endpoint error format — the
  frontend depends on `ErrorResponse` being uniform.

## Reference map

| Concern | Real file |
| --- | --- |
| The handler | [`GlobalExceptionHandler.cs`](../../../src/JobBoard.Shared/Errors/GlobalExceptionHandler.cs) |
| Domain exception base | [`DomainException.cs`](../../../src/JobBoard.Shared/Errors/DomainException.cs) |
| The error shape | [`ErrorResponse.cs`](../../../src/JobBoard.Shared/Errors/ErrorResponse.cs) · [`ErrorDetail.cs`](../../../src/JobBoard.Shared/Errors/ErrorDetail.cs) |
| Registration + activation | [`SharedServiceCollectionExtensions.cs`](../../../src/JobBoard.Shared/DependencyInjection/SharedServiceCollectionExtensions.cs) (`AddSharedExceptionHandler`) |

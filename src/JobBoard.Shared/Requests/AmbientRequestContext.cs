namespace JobBoard.Shared.Requests;

/// <summary>
/// Scoped implementation of <see cref="IRequestContext"/>. Starts empty and is populated once per request
/// by <see cref="RequestContextMiddleware"/> via <see cref="Populate"/>; everything else only reads it.
/// </summary>
public sealed class AmbientRequestContext : IRequestContext
{
    /// <inheritdoc/>
    public Guid CorrelationId { get; private set; }

    /// <inheritdoc/>
    public Guid? ActorId { get; private set; }

    /// <inheritdoc/>
    public string? ActorRole { get; private set; }

    /// <summary>Fills the context from the values the edge projected. Called once, by the middleware.</summary>
    public void Populate(Guid correlationId, Guid? actorId, string? actorRole)
    {
        CorrelationId = correlationId;
        ActorId = actorId;
        ActorRole = actorRole;
    }
}

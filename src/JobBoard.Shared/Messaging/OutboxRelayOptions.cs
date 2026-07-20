namespace JobBoard.Shared.Messaging;

/// <summary>
/// Tunables for the <see cref="OutboxDispatcher"/> loop. Defaults suit local development; a host can
/// override them through <c>AddSharedMessaging&lt;TDbContext&gt;(o =&gt; ...)</c>.
/// </summary>
public sealed class OutboxRelayOptions
{
    /// <summary>How long the dispatcher waits between polls of the outbox table.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum number of unprocessed rows relayed per poll, oldest-first.</summary>
    public int BatchSize { get; set; } = 20;
}

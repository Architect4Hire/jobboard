namespace JobBoard.Jobs.Core.Managers.Models.Domain;

/// <summary>
/// Lifecycle of a <see cref="Job"/>. Persisted as its underlying <c>int</c> (the EF default), so the
/// numeric values are the contract — append new states, never renumber existing ones.
/// <c>Open</c> is what a <c>JobPosted</c> event will announce; <c>Closed</c> a <c>JobClosed</c>.
/// </summary>
public enum JobStatus
{
    Draft = 0,
    Open = 1,
    Closed = 2,
}

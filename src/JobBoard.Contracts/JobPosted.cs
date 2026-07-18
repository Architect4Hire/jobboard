namespace JobBoard.Contracts;

/// <summary>
/// Fact: a job was posted (opened). Published by Jobs from the post-job endpoint via its outbox, and
/// consumed by Notifications to log a confirmation. Carries the minimum denormalized data a consumer
/// needs (title + location) so no call-back to Jobs is required.
/// </summary>
public sealed record JobPosted(
    Guid Id,
    Guid JobId,
    Guid EmployerId,
    string Title,
    string Location,
    DateTime PostedOnUtc) : IIntegrationEvent;

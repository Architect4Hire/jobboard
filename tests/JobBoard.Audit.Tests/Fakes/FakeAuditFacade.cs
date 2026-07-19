using JobBoard.Audit.Core.Facade;
using JobBoard.Audit.Core.Managers.Models.ServiceModels;
using JobBoard.Audit.Core.Managers.Models.ViewModels;
using JobBoard.Contracts;

namespace JobBoard.Audit.Tests.Fakes;

/// <summary>Hand-rolled <see cref="IAuditFacade"/> for consumer tests: captures the event it was
/// handed, so a test can assert the thin consumer forwarded it unchanged. The read side is unused here.</summary>
public sealed class FakeAuditFacade : IAuditFacade
{
    public IIntegrationEvent? Recorded { get; private set; }

    public Task RecordAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        Recorded = @event;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntryServiceModel>> QueryAsync(
        AuditQueryViewModel query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AuditEntryServiceModel>>([]);
}

using JobBoard.Contracts;
using JobBoard.Profiles.Core.Managers.Models.Domain;

namespace JobBoard.Profiles.Core.Data;

/// <summary>
/// Composes the employer-profile repository into whole operations and owns the transaction boundary. The
/// read passes straight through; the upsert commits inside a transaction — enqueuing the
/// <see cref="ProfileUpdated"/> outbox row in the same unit so the event ships iff the write commits — and
/// maps a concurrent-insert primary-key collision to a retryable conflict. Depends on
/// <see cref="IEmployerProfileRepository"/> and <c>IOutbox</c>; holds no <c>DbContext</c>.
/// </summary>
public interface IEmployerProfileDataLayer
{
    Task<EmployerProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmployerProfile> UpsertAsync(EmployerProfile incoming, ProfileUpdated updated, CancellationToken cancellationToken = default);
}

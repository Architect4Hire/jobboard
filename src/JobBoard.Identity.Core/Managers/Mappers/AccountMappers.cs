using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Identity.Core.Managers.Models.ServiceModels;
using JobBoard.Identity.Core.Managers.Models.ViewModels;

namespace JobBoard.Identity.Core.Managers.Mappers;

/// <summary>
/// The two mapping seams the business layer owns: <b>ViewModel → Domain</b> (register, taking the
/// already-computed password hash) and <b>Domain → ServiceModel</b> (the register response). There is
/// no domain → integration event mapping here — Identity publishes no events in this scope.
/// </summary>
public static class AccountMappers
{
    /// <summary>
    /// Translates the register request into a new <see cref="Account"/>. The password is hashed by the
    /// business layer and passed in as <paramref name="passwordHash"/> — the plaintext never reaches an
    /// entity.
    /// </summary>
    public static Account ToEntity(this RegisterAccountViewModel vm, string passwordHash) => new()
    {
        Id = Guid.NewGuid(),
        Email = vm.Email,
        PasswordHash = passwordHash,
        Role = vm.Role,
        CreatedOnUtc = DateTime.UtcNow,
    };

    /// <summary>Maps a persisted account to the register response shape (never the password hash).</summary>
    public static AccountServiceModel ToServiceModel(this Account account) =>
        new(account.Id, account.Email, account.Role);
}

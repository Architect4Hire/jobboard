using JobBoard.Contracts;
using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Identity.Core.Managers.Models.ServiceModels;
using JobBoard.Identity.Core.Managers.Models.ViewModels;
using JobBoard.Shared.Requests;

namespace JobBoard.Identity.Core.Managers.Mappers;

/// <summary>
/// The mapping seams the business layer owns: <b>ViewModel → Domain</b> (register, taking the
/// already-computed password hash), <b>Domain → ServiceModel</b> (the register response), and
/// <b>Domain → integration event</b> (the audit facts registration and login emit). The event mappers
/// stamp a fresh event id and the audit <c>thread</c> (ADR-0013) and carry only non-secret fields — never
/// the password or its hash.
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

    /// <summary>
    /// Builds the <see cref="AccountCreated"/> fact for a just-registered account, stamping a fresh event id
    /// (its outbox-row key and Service Bus <c>MessageId</c>) and the audit <paramref name="thread"/>
    /// (ADR-0013). Carries the email and role the trail needs to identify the account — never the password
    /// hash. The role is persisted as its enum name, so the string is stable across versions.
    /// </summary>
    public static AccountCreated ToAccountCreated(this Account account, AuditThread thread) =>
        new(Guid.NewGuid(), account.Id, account.Email, account.Role.ToString(), account.CreatedOnUtc)
        {
            CorrelationId = thread.CorrelationId,
            CausationId = thread.CausationId,
            ActorId = thread.ActorId,
        };

    /// <summary>
    /// Builds the <see cref="LoggedIn"/> fact for a just-authenticated account, stamping a fresh event id and
    /// the audit <paramref name="thread"/> (ADR-0013). The occurred-at is the sign-in moment (login persists
    /// nothing of its own). Carries the email and role only — never the password or the issued token.
    /// </summary>
    public static LoggedIn ToLoggedIn(this Account account, AuditThread thread) =>
        new(Guid.NewGuid(), account.Id, account.Email, account.Role.ToString(), DateTime.UtcNow)
        {
            CorrelationId = thread.CorrelationId,
            CausationId = thread.CausationId,
            ActorId = thread.ActorId,
        };

    /// <summary>
    /// Builds the <see cref="LoginFailed"/> fact for a rejected login. Built from the attempted
    /// <paramref name="email"/> rather than an <see cref="Account"/> — an unknown email has no account — with
    /// a uniform <paramref name="reason"/> so the trail can't reveal whether the email exists. Correlation and
    /// causation come from the <paramref name="thread"/>, but the actor is forced to <c>null</c>: a rejected
    /// login has no authenticated identity, so the event never attributes one even if the context carried one.
    /// </summary>
    public static LoginFailed ToLoginFailed(string email, AuditThread thread, string reason = "invalid_credentials") =>
        new(Guid.NewGuid(), email, reason, DateTime.UtcNow)
        {
            CorrelationId = thread.CorrelationId,
            CausationId = thread.CausationId,
            ActorId = null,
        };
}

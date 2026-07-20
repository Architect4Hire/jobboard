using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobBoard.Identity.Core.Data;

/// <summary>
/// EF Core implementation of <see cref="IAccountRepository"/> over <see cref="IdentityDbContext"/>.
/// Inherits <c>ExecuteInTransactionAsync</c> from <see cref="BaseRepository{TContext}"/>.
/// </summary>
public sealed class AccountRepository : BaseRepository<IdentityDbContext>, IAccountRepository
{
    public AccountRepository(IdentityDbContext context) : base(context)
    {
    }

    public Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Email == email, cancellationToken);

    public async Task<Account> AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        await Context.Accounts.AddAsync(account, cancellationToken);
        return account;
    }

    /// <summary>
    /// True when <paramref name="exception"/> is the unique-index violation on <c>Email</c> — two
    /// registrations racing on the same brand-new address in the window between the caller's read and its
    /// insert. The classifier lives here (the repository owns provider knowledge), but the exception
    /// surfaces from <c>SaveChanges</c> inside
    /// <see cref="BaseRepository{TContext}.ExecuteInTransactionAsync{T}"/>, so the data layer — which owns
    /// the transaction — is where it's caught and mapped to a conflict.
    /// </summary>
    public static bool IsDuplicateEmailViolation(DbUpdateException exception) =>
        exception.InnerException switch
        {
            // Npgsql (production): a unique_violation (23505) whose failing index is the Email index.
            PostgresException pg => pg.SqlState == PostgresErrorCodes.UniqueViolation
                && (pg.ConstraintName?.Contains("Email", StringComparison.OrdinalIgnoreCase) ?? false),
            // Other providers (e.g. SQLite in tests) name the offending Email column in the failure text.
            { } inner => inner.Message.Contains("Email", StringComparison.OrdinalIgnoreCase)
                && inner.Message.Contains("unique", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
}

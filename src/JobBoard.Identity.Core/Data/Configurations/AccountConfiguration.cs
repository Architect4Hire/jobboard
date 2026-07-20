using JobBoard.Identity.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobBoard.Identity.Core.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Account"/>: app-assigned key, a <b>unique</b> index on
/// <see cref="Account.Email"/> (the login key and the guard registration races trip), and
/// <see cref="AccountRole"/> stored as its string name so the persisted value is self-describing.
/// </summary>
internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(a => a.Email).IsUnique();

        builder.Property(a => a.PasswordHash).IsRequired();

        builder.Property(a => a.Role).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(a => a.CreatedOnUtc).IsRequired();
    }
}

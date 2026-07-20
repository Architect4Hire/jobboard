using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Shared.Tests;

/// <summary>A concrete <see cref="BaseDbContext"/> with no domain sets — enough to exercise the spine.</summary>
public sealed class TestDbContext : BaseDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }
}

using JobBoard.Shared.Persistence;

namespace JobBoard.Shared.Tests;

/// <summary>A concrete repository over <see cref="TestDbContext"/> to reach the base transaction seam.</summary>
public sealed class TestRepository : BaseRepository<TestDbContext>
{
    public TestRepository(TestDbContext context) : base(context)
    {
    }
}

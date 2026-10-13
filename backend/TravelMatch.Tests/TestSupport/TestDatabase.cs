
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;

namespace TravelMatch.Tests.TestSupport;

public static class TestDatabase
{
    public static ApplicationDbContext CreateContext()
    {
        return CreateContext($"travelmatch-{Guid.NewGuid()}");
    }

    public static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        return context;
    }
}

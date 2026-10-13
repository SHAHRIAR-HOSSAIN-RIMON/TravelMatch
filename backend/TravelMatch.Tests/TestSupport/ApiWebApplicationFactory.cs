
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TravelMatch.API.Data;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;
using TravelMatch.API.Options;
using TravelMatch.API.Services;

namespace TravelMatch.Tests.TestSupport;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string JwtSecret = "travelmatch-test-signing-key-0123456789abcdef";
    public const string JwtIssuer = "TravelMatch.Tests";
    public const string JwtAudience = "TravelMatch.Tests.Client";

    private readonly string _databaseName = $"travelmatch-api-{Guid.NewGuid()}";

    public string DatabaseName => _databaseName;

    public string GenerateToken(User user)
    {
        var jwtTokenService = new JwtTokenService(
            Options.Create(new JwtSettings
            {
                Secret = JwtSecret,
                Issuer = JwtIssuer,
                Audience = JwtAudience,
                ExpiryMinutes = 60
            }));

        return jwtTokenService.GenerateToken(user);
    }

    public ApplicationDbContext CreateContext()
    {
        return TestDatabase.CreateContext(_databaseName);
    }

    public void SeedAction(Action<ApplicationDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        seed(context);
    }

    public void ResetDatabase()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        context.TripRequests.RemoveRange(context.TripRequests);
        context.TouristProfiles.RemoveRange(context.TouristProfiles);
        context.Users.RemoveRange(context.Users);
        context.SaveChanges();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = JwtSecret,
                ["JwtSettings:Issuer"] = JwtIssuer,
                ["JwtSettings:Audience"] = JwtAudience,
                ["JwtSettings:ExpiryMinutes"] = "60"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();

            foreach (var descriptor in services
                .Where(descriptor =>
                    descriptor.ServiceType.IsGenericType &&
                    descriptor.ServiceType.GetGenericTypeDefinition() ==
                    typeof(IDbContextOptionsConfiguration<>))
                .ToList())
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}

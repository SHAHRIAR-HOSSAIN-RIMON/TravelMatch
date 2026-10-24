using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.Hubs;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Options;
using TravelMatch.API.Services;

namespace TravelMatch.API.Tests;

/// <summary>
/// Minimal real ASP.NET Core host (routing, JWT auth, controllers, SignalR) backed by an
/// isolated EF InMemory database. Used for HTTP-pipeline and SignalR-delivery integration tests.
/// </summary>
internal sealed class TestApplication : IAsyncDisposable
{
    private const string JwtIssuer = "TravelMatch.Tests";
    private const string JwtAudience = "TravelMatch.Tests.Clients";
    private const string JwtSecret = "travelmatch-integration-tests-signing-key-0123456789";

    private readonly WebApplication _app;

    private TestApplication(WebApplication app)
    {
        _app = app;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public static async Task<TestApplication> StartAsync()
    {
        var databaseName = $"TravelMatchIntegration-{Guid.NewGuid():N}";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.Configure<JwtSettings>(settings =>
        {
            settings.Issuer = JwtIssuer;
            settings.Audience = JwtAudience;
            settings.Secret = JwtSecret;
            settings.ExpiryMinutes = 60;
        });

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = JwtIssuer,
                    ValidAudience = JwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(JwtSecret))
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddSignalR();

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
        builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IRegistrationService, RegistrationService>();
        builder.Services.AddScoped<IOrganizedTripService, OrganizedTripService>();
        builder.Services.AddScoped<ISandboxPaymentGateway, SandboxPaymentGateway>();
        builder.Services.AddScoped<IPaymentService, PaymentService>();
        builder.Services.AddScoped<IReviewService, ReviewService>();
        builder.Services.AddScoped<IProfileService, ProfileService>();
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddScoped<INotificationRealtimeSender, SignalRNotificationSender>();
        builder.Services.AddScoped<IDisputeService, DisputeService>();

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<NotificationHub>("/hubs/notifications");

        await app.StartAsync();

        return new TestApplication(app);
    }

    public HttpMessageHandler CreateServerHandler() => _app.GetTestServer().CreateHandler();

    public void UseToken(string token) =>
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public void ClearToken() => Client.DefaultRequestHeaders.Authorization = null;

    public async Task<string> CreateTokenAsync(int userId)
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        return scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateToken(user);
    }

    public async Task SeedAsync(Func<ApplicationDbContext, Task> seed)
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }

    public async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> read)
    {
        using var scope = _app.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

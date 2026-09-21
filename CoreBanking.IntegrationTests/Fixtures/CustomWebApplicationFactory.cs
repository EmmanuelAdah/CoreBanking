using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using CoreBanking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreBanking.IntegrationTests.Fixtures;

/// <summary>
/// Boots the real API with an in-memory EF Core database and relaxed external dependencies.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("PAYSTACK_SECRET_KEY", "sk_test_dummy");
        builder.UseSetting("JWT_SECRET", "ChangeThisToAVeryLongRandomSecretKeyAtLeast32CharactersLong!");
        builder.UseSetting("JWT_ISSUER", "CoreBanking");
        builder.UseSetting("JWT_AUDIENCE", "CoreBankingClients");

        builder.ConfigureTestServices(services =>
        {
            // Replace Postgres with EF InMemory for isolated e2e tests
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<BankingDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.RemoveAll(typeof(DbContextOptions<BankingDbContext>));

            services.AddDbContext<BankingDbContext>(options =>
            {
                options.UseInMemoryDatabase("CoreBankingTests_" + Guid.NewGuid());
            });

            // Ensure JWT secret is present for token generation/validation
            // (Program.cs already falls back to a default secret if env is missing)
        });
    }

    public HttpClient CreateAuthenticatedClient(string role = "Customer", string email = "test@bank.com")
    {
        // Prefer real login flow in tests; this helper is for endpoints that only need a bearer token shape.
        return CreateClient();
    }

    public async Task EnsureDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
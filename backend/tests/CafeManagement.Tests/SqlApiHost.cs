using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using CafeManagement.Infrastructure.Persistence;
using Xunit;

namespace CafeManagement.Tests;

internal sealed class SqlApiHost : WebApplicationFactory<Program>
{
    private readonly string connection;
    private readonly IReadOnlyDictionary<string, string?> settings;
    private readonly string key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
    private readonly string? previousKey = Environment.GetEnvironmentVariable("Jwt__Key");

    private SqlApiHost(string connection, IReadOnlyDictionary<string, string?>? settings)
    {
        this.connection = connection;
        this.settings = settings ?? new Dictionary<string, string?>();
        // Bootstrap validation runs before the test host's configuration callback.
        Environment.SetEnvironmentVariable("Jwt__Key", key);
    }

    public static async Task<SqlApiHost> CreateAsync(
        string phase = "Phase2", IReadOnlyDictionary<string, string?>? settings = null)
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CAFE_TEST_SQL"))
        {
            InitialCatalog = $"CafeManagement_Test_{phase}_{Guid.NewGuid():N}"
        };
        var host = new SqlApiHost(builder.ConnectionString, settings);
        try
        {
            await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>()
                .UseSqlServer(builder.ConnectionString).Options);
            await db.Database.MigrateAsync();
            using var client = host.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:CafeDatabase"] = connection, ["Jwt:Key"] = key,
                ["Seed:Enabled"] = "true",
                ["Seed:Passwords:admin"] = "Admin@123", ["Seed:Passwords:manager"] = "Manager@123",
                ["Seed:Passwords:waiter"] = "Waiter@123", ["Seed:Passwords:barista"] = "Barista@123",
                ["Seed:Passwords:cashier"] = "Cashier@123", ["Seed:Passwords:warehouse"] = "Warehouse@123"
            };
            if (this.settings.Count > 0)
                foreach (var setting in this.settings)
                    values[setting.Key] = setting.Value;
            configuration.AddInMemoryCollection(values);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        try
        {
            await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(connection).Options);
            await db.Database.EnsureDeletedAsync();
        }
        finally { Environment.SetEnvironmentVariable("Jwt__Key", previousKey); }
    }
}

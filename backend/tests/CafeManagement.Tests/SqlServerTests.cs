using CafeManagement.Domain.Entities;
using CafeManagement.Infrastructure;
using CafeManagement.Infrastructure.Persistence;
using CafeManagement.Infrastructure.Seeding;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CafeManagement.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CAFE_TEST_SQL")))
            Skip = "Set CAFE_TEST_SQL to a SQL Server connection with permission to create a temporary database.";
    }
}

public sealed class SqlServerTests
{
    [SqlServerFact]
    public async Task MigrationAndDevelopmentSeedCreateTheDiagramModel()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CAFE_TEST_SQL"))
        {
            InitialCatalog = $"CafeManagement_Test_{Guid.NewGuid():N}"
        }.ConnectionString;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:CafeDatabase"] = connection,
                ["Seed:Passwords:admin"] = "Admin@123",
                ["Seed:Passwords:manager"] = "Manager@123",
                ["Seed:Passwords:waiter"] = "Waiter@123",
                ["Seed:Passwords:barista"] = "Barista@123",
                ["Seed:Passwords:cashier"] = "Cashier@123",
                ["Seed:Passwords:warehouse"] = "Warehouse@123"
            }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(connection).Options;
        await using var cleanup = new CafeDbContext(options);
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CafeDbContext>();
            await db.Database.MigrateAsync();
            var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>();
            await seeder.SeedAsync();
            await seeder.SeedAsync();

            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(6, await db.TaiKhoans.CountAsync());
            Assert.Equal(6, await db.NhanViens.CountAsync());
            Assert.Equal(6, await db.VaiTros.CountAsync());
            Assert.Equal(10, await db.Bans.CountAsync());
            Assert.Equal(4, await db.DanhMucs.CountAsync());
            Assert.Equal(6, await db.Mons.CountAsync());
            Assert.Equal(12, await db.KichCos.CountAsync());
            Assert.Equal(12, await db.Toppings.CountAsync());
            Assert.Equal(6, await db.DinhLuongs.CountAsync());
            Assert.Equal(4, await db.NguyenLieus.CountAsync());
            Assert.Equal(4, await db.BienDongKhos.CountAsync());
            Assert.Equal(1, await db.KhuyenMais.CountAsync());
            Assert.All(await db.TaiKhoans.ToListAsync(), x => Assert.False(string.IsNullOrWhiteSpace(x.MatKhauHash)));
            Assert.Equal("Cà phê", (await db.DanhMucs.SingleAsync(x => x.MaDanhMuc == "DM01")).TenDanhMuc);
            Assert.Equal("Cà phê sữa", (await db.Mons.SingleAsync(x => x.MaMon == "MON02")).TenMon);
        }
        finally
        {
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CafeManagement.Infrastructure.Persistence;

public sealed class CafeDbContextFactory : IDesignTimeDbContextFactory<CafeDbContext>
{
    public CafeDbContext CreateDbContext(string[] args)
    {
        // Generating migrations does not connect. Database update must use a real connection.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__CafeDatabase")
            ?? "Server=YOUR_SQL_SERVER;Database=CafeManagement;Integrated Security=True;Encrypt=True";
        var options = new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(connection).Options;
        return new CafeDbContext(options);
    }
}

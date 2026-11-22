using CafeManagement.Domain.Entities;
using CafeManagement.Infrastructure.Persistence;
using CafeManagement.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CafeManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CafeDbContext>(options =>
        {
            var connection = configuration.GetConnectionString("CafeDatabase")
                ?? throw new InvalidOperationException("ConnectionStrings:CafeDatabase is required.");
            options.UseSqlServer(connection);
        });
        services.AddScoped<IdGenerator>();
        services.AddScoped<IPasswordHasher<TaiKhoan>, PasswordHasher<TaiKhoan>>();
        services.AddScoped<DevelopmentSeeder>();
        return services;
    }
}

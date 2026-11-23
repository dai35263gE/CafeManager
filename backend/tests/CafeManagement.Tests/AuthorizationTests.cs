using System.Reflection;
using System.Security.Claims;
using CafeManagement.Api.Security;
using CafeManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CafeManagement.Tests;

public sealed class AuthorizationTests
{
    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Barista)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Warehouse)]
    public async Task AdminCanAccessEveryRoleProtectedActionWhileOtherRolesKeepTheirRestrictions(string role)
    {
        using var services = CreateServices();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var policies = services.GetRequiredService<IAuthorizationPolicyProvider>();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)], "Test"));
        var checkedActions = 0;

        foreach (var controller in typeof(Program).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract))
        {
            foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any()))
            {
                var attributes = controller.GetCustomAttributes<AuthorizeAttribute>()
                    .Concat(action.GetCustomAttributes<AuthorizeAttribute>()).ToArray();
                if (!attributes.Any(attribute => !string.IsNullOrWhiteSpace(attribute.Roles)))
                    continue;

                var policy = await AuthorizationPolicy.CombineAsync(policies, attributes);
                Assert.NotNull(policy);
                var expected = role == Roles.Admin || attributes.All(attribute =>
                    string.IsNullOrWhiteSpace(attribute.Roles) ||
                    attribute.Roles.Split(',').Select(value => value.Trim()).Contains(role));
                var result = await authorization.AuthorizeAsync(user, null, policy);
                Assert.True(result.Succeeded == expected, $"{role}: {controller.Name}.{action.Name}");
                checkedActions++;
            }
        }

        Assert.True(checkedActions > 0);
    }

    [Fact]
    public async Task AnonymousAdminClaimDoesNotGrantRoleAccessOrBypassOtherRequirements()
    {
        using var services = CreateServices();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var claim = new Claim(ClaimTypes.Role, Roles.Admin);
        var rolePolicy = new AuthorizationPolicyBuilder().RequireRole(Roles.Manager).Build();
        Assert.False((await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity([claim])), null, rolePolicy)).Succeeded);

        var admin = new ClaimsPrincipal(new ClaimsIdentity([claim], "Test"));
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
            .RequireRole(Roles.Manager).RequireClaim("special-permission").Build();
        Assert.False((await authorization.AuthorizeAsync(admin, null, policy)).Succeeded);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationHandler, AdminRoleAuthorizationHandler>();
        return services.BuildServiceProvider();
    }
}

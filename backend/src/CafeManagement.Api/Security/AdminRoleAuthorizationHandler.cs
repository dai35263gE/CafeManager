using CafeManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace CafeManagement.Api.Security;

public sealed class AdminRoleAuthorizationHandler : AuthorizationHandler<RolesAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RolesAuthorizationRequirement requirement)
    {
        if (context.User.Identities.Any(identity => identity.IsAuthenticated) &&
            context.User.IsInRole(Roles.Admin))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

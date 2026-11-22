using CafeManagement.Domain.Common;

namespace CafeManagement.Api.Security;

public static class AccessRoles
{
    public const string Manager = Roles.Manager;
    public const string Operations = Roles.Manager + "," + Roles.Waiter + "," + Roles.Barista + "," + Roles.Cashier;
    public const string Floor = Roles.Manager + "," + Roles.Waiter;
    public const string Kitchen = Roles.Manager + "," + Roles.Barista;
}

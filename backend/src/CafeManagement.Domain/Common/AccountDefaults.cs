namespace CafeManagement.Domain.Common;

public static class AccountDefaults
{
    public static DateTime NeverLoggedIn { get; } = new(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}

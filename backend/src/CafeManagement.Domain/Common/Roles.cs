namespace CafeManagement.Domain.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Waiter = "Waiter";
    public const string Barista = "Barista";
    public const string Cashier = "Cashier";
    public const string Warehouse = "Warehouse";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly(new[] { Admin, Manager, Waiter, Barista, Cashier, Warehouse });

    // Role id (ma_vai_tro) and permission list (danh_sach_quyen) seeded into VaiTro.
    public static IReadOnlyDictionary<string, (string Id, string Permissions)> Definitions { get; } =
        new Dictionary<string, (string, string)>
        {
            [Admin] = ("VT01", "users.manage,employees.manage"),
            [Manager] = ("VT02", "menu.manage,tables.manage,orders.manage,promotions.manage,invoices.manage,inventory.manage,reports.view,employees.manage"),
            [Waiter] = ("VT03", "orders.create,orders.serve,tables.view,menu.view"),
            [Barista] = ("VT04", "kitchen.prepare,menu.availability,inventory.view,notifications.view"),
            [Cashier] = ("VT05", "invoices.create,promotions.apply,orders.view"),
            [Warehouse] = ("VT06", "inventory.manage,reports.view")
        };
}

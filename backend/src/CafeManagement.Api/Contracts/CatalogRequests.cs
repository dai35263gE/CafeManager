using System.ComponentModel.DataAnnotations;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Contracts;

/// <summary>Table creation and editable profile.</summary>
public sealed record SaveTableRequest
{
    [Required, MaxLength(10)] public required string Code { get; init; }
    [Required, MaxLength(50)] public required string Area { get; init; }
    [MaxLength(255)] public string? Note { get; init; }
    public string? RowVersion { get; init; }
}

/// <summary>A floor table with concurrency token.</summary>
public sealed record TableResponse(
    string Id, string Code, string Area, TableStatus Status, string? Note, string RowVersion,
    ActiveTableOrderResponse? ActiveOrder);

public sealed record ActiveTableOrderResponse(string Id, string Code, OrderStatus Status, string RowVersion);

/// <summary>Cleaning or disabling a table, without overriding occupancy.</summary>
public sealed record TableStatusRequest
{
    [EnumDataType(typeof(TableStatus))] public TableStatus Status { get; init; }
    [Required] public required string RowVersion { get; init; }
}

public sealed record SeatTableRequest
{
    [Required] public required string RowVersion { get; init; }
}

/// <summary>Category creation or update.</summary>
public sealed record SaveCategoryRequest
{
    [Required, MaxLength(100)] public required string Name { get; init; }
    [MaxLength(500)] public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>A menu category.</summary>
public sealed record CategoryResponse(string Id, string Name, string? Description, bool IsActive);

/// <summary>Reusable size creation or update.</summary>
public sealed record SaveSizeRequest
{
    [Required, MaxLength(10)] public required string MenuItemId { get; init; }
    [Required, MaxLength(50)] public required string Name { get; init; }
    [Range(typeof(decimal), "0", "999999999")] public decimal PriceDelta { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>A reusable size definition, not a menu item's final price.</summary>
public sealed record SizeResponse(string Id, string MenuItemId, string Name, decimal PriceDelta, bool IsActive);

/// <summary>Topping creation or update.</summary>
public sealed record SaveToppingRequest
{
    [Required, MaxLength(10)] public required string MenuItemId { get; init; }
    [Required, MaxLength(100)] public required string Name { get; init; }
    [Range(typeof(decimal), "0", "999999999")] public decimal Price { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>A topping definition.</summary>
public sealed record ToppingResponse(string Id, string MenuItemId, string Name, decimal Price, bool IsActive);

/// <summary>Create or update one KichCo option for a Mon.</summary>
public sealed record MenuSizeRequest
{
    [MaxLength(10)] public string? SizeId { get; init; }
    [MaxLength(20)] public string? Name { get; init; }
    [Range(typeof(decimal), "0", "999999999")] public decimal Price { get; init; }
}

/// <summary>Create or update one Topping option for a Mon.</summary>
public sealed record MenuToppingRequest
{
    [MaxLength(10)] public string? ToppingId { get; init; }
    [MaxLength(100)] public string? Name { get; init; }
    [Range(typeof(decimal), "0", "999999999")] public decimal Price { get; init; }
}

/// <summary>Menu creation or replacement of editable fields and permitted options.</summary>
public sealed record SaveMenuItemRequest
{
    [Required, MaxLength(10)] public required string CategoryId { get; init; }
    [Required, MaxLength(100)] public required string Name { get; init; }
    [MaxLength(2000)] public string? Description { get; init; }
    [Url, MaxLength(255)] public string? ImageUrl { get; init; }
    [Range(typeof(decimal), "0", "999999999")] public decimal BasePrice { get; init; }
    [EnumDataType(typeof(MenuItemStatus))] public MenuItemStatus Status { get; init; } = MenuItemStatus.Available;
    public bool IsActive { get; init; } = true;
    [MaxLength(20)] public IReadOnlyList<MenuSizeRequest>? Sizes { get; init; }
    [MaxLength(50)] public IReadOnlyList<MenuToppingRequest>? Toppings { get; init; }
    [MaxLength(50)] public IReadOnlyList<string> ToppingIds { get; init; } = [];
    public string? RowVersion { get; init; }
}

/// <summary>A menu item's priced size.</summary>
public sealed record MenuSizeResponse(string SizeId, string Name, decimal Price, bool IsActive);

/// <summary>A menu item, with priced sizes and allowed toppings.</summary>
public sealed record MenuItemResponse(string Id, string CategoryId, string CategoryName, string Name,
    string? Description, string? ImageUrl, decimal BasePrice, MenuItemStatus Status, bool IsActive,
    IReadOnlyList<MenuSizeResponse> Sizes, IReadOnlyList<ToppingResponse> Toppings, string RowVersion);

/// <summary>Change menu availability using the current Mon concurrency token.</summary>
public sealed record MenuStatusRequest
{
    [EnumDataType(typeof(MenuItemStatus))] public MenuItemStatus Status { get; init; }
    [Required] public required string RowVersion { get; init; }
}

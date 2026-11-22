using System.ComponentModel.DataAnnotations;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Contracts;

/// <summary>Topping parts per drink, priced by the server.</summary>
public sealed record OrderToppingRequest(string ToppingId, [Range(1, 20)] int Quantity = 1);

/// <summary>A new order line with server-calculated price snapshots.</summary>
public sealed record CreateOrderItemRequest
{
    [Required, MaxLength(10)] public required string MenuItemId { get; init; }
    public string? SizeId { get; init; }
    [Range(1, 100)] public int Quantity { get; init; } = 1;
    [MaxLength(2000)] public string? Note { get; init; }
    [Required, MaxLength(50)] public IReadOnlyList<OrderToppingRequest> Toppings { get; init; } = [];
}

/// <summary>Create a dine-in or takeaway order, optionally confirming it with initial items.</summary>
public sealed record CreateOrderRequest
{
    [EnumDataType(typeof(OrderType))] public OrderType Type { get; init; }
    public string? TableId { get; init; }
    [MaxLength(2000)] public string? Note { get; init; }
    public bool Confirm { get; init; }
    [Required, MaxLength(100)] public IReadOnlyList<CreateOrderItemRequest> Items { get; init; } = [];
}

/// <summary>Add one or more new lines using the current aggregate concurrency token.</summary>
public sealed record AddOrderItemsRequest
{
    [Required] public required string RowVersion { get; init; }
    [Required, MinLength(1), MaxLength(100)] public required IReadOnlyList<CreateOrderItemRequest> Items { get; init; }
}

/// <summary>Change quantity/note only before preparation; preserve selected options and prices.</summary>
public sealed record UpdateOrderItemRequest
{
    [Range(1, 100)] public int Quantity { get; init; }
    [MaxLength(2000)] public string? Note { get; init; }
    [Required] public required string RowVersion { get; init; }
}

/// <summary>Update an order note without replacing its sales lines.</summary>
public sealed record UpdateOrderRequest
{
    [MaxLength(2000)] public string? Note { get; init; }
    [Required] public required string RowVersion { get; init; }
}

/// <summary>Confirm, cancel or acknowledge all served items. Paid is exclusively a payment action.</summary>
public sealed record OrderStatusRequest
{
    [EnumDataType(typeof(OrderStatus))] public OrderStatus Status { get; init; }
    [Required] public required string RowVersion { get; init; }
    [MaxLength(1000)] public string? Reason { get; init; }
}

/// <summary>Advance a kitchen line or acknowledge service using its concurrency token.</summary>
public sealed record ItemStatusRequest
{
    [EnumDataType(typeof(OrderItemStatus))] public OrderItemStatus Status { get; init; }
    [Required] public required string RowVersion { get; init; }
    [MaxLength(1000)] public string? Reason { get; init; }
}

/// <summary>Cancel a line with an auditable reason.</summary>
public sealed record CancelItemRequest
{
    [Required] public required string RowVersion { get; init; }
    [Required, MaxLength(1000)] public required string Reason { get; init; }
}

/// <summary>Allow a manager to return a cancelled line to the waiting queue with an audit reason.</summary>
public sealed record ReopenOrderItemRequest
{
    [Required] public required string RowVersion { get; init; }
    [Required, MaxLength(1000)] public required string Reason { get; init; }
}

/// <summary>Move a dine-in order to a currently available table.</summary>
public sealed record TransferOrderRequest
{
    [Required, MaxLength(10)] public required string TargetTableId { get; init; }
    [Required] public required string RowVersion { get; init; }
    [Required] public required string SourceTableRowVersion { get; init; }
    [Required] public required string TargetTableRowVersion { get; init; }
}

/// <summary>Table-oriented transfer entry point sharing the order transaction.</summary>
public sealed record TransferTableRequest
{
    [Required, MaxLength(10)] public required string SourceTableId { get; init; }
    [Required, MaxLength(10)] public required string TargetTableId { get; init; }
    [Required] public required string OrderRowVersion { get; init; }
    [Required] public required string SourceTableRowVersion { get; init; }
    [Required] public required string TargetTableRowVersion { get; init; }
}

/// <summary>Merge this source order into another dine-in order, preserving line snapshots.</summary>
public sealed record MergeOrderRequest
{
    [Required, MaxLength(10)] public required string TargetOrderId { get; init; }
    [Required] public required string RowVersion { get; init; }
    [Required] public required string TargetOrderRowVersion { get; init; }
}

public sealed record TableMergeSourceRequest
{
    [Required, MaxLength(10)] public required string TableId { get; init; }
    [Required] public required string TableRowVersion { get; init; }
    public string? ActiveOrderRowVersion { get; init; }
}

/// <summary>Merge one or more available/occupied tables into the selected main table.</summary>
public sealed record MergeTableRequest
{
    [Required, MaxLength(10)] public required string TargetTableId { get; init; }
    [Required] public required string TargetTableRowVersion { get; init; }
    [Required, MinLength(1), MaxLength(19)] public required IReadOnlyList<TableMergeSourceRequest> Sources { get; init; }
    [MaxLength(10)] public string? TargetOrderId { get; init; }
    public string? TargetOrderRowVersion { get; init; }
    public string? TargetActiveOrderRowVersion { get; init; }
}

public sealed record MergeTablesResponse(
    string TargetTableId, IReadOnlyList<string> SourceTableIds, OrderResponse? ActiveOrder);

/// <summary>A topping sales snapshot.</summary>
public sealed record OrderToppingResponse(string ToppingId, string Name, int Quantity, decimal UnitPrice);

/// <summary>A sales line and its preparation owner/concurrency token.</summary>
public sealed record OrderItemResponse(string Id, string MenuItemId, string MenuItemName, string? SizeId, string? SizeName,
    int Quantity, decimal UnitPrice, decimal LineTotal, string? Note, OrderItemStatus Status,
    string? PreparedByUserId, string RowVersion, IReadOnlyList<OrderToppingResponse> Toppings);

/// <summary>An order with current lines and total snapshots.</summary>
public sealed record OrderResponse(string Id, string Code, OrderType Type, string? TableId, string? TableCode,
    OrderStatus Status, string CreatedByUserId, DateTimeOffset CreatedAt, string? Note,
    decimal SubTotal, string RowVersion, IReadOnlyList<OrderItemResponse> Items);

/// <summary>A preparation line, including cancellations and current assignee.</summary>
public sealed record KitchenItemResponse(string OrderId, string OrderCode, string? TableId, string? TableCode,
    string? OrderNote, OrderStatus OrderStatus, DateTimeOffset CreatedAt, OrderItemResponse Item);

/// <summary>Persisted shortage report; does not alter physical stock.</summary>
public sealed record ShortageReportRequest
{
    public string? MenuItemId { get; init; }
    public string? IngredientId { get; init; }
    [MaxLength(1000)] public string? Reason { get; init; }
}

/// <summary>An auditable shortage notice suitable for polling.</summary>
public sealed record KitchenNoticeResponse(string Id, string? UserId, string? EntityId,
    string Description, bool IsRead, DateTimeOffset CreatedAt);

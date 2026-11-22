using System.ComponentModel.DataAnnotations;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Contracts;

/// <summary>Ingredient metadata; stock changes must use movement endpoints.</summary>
public sealed record SaveIngredientRequest
{
    [Required, MaxLength(10)] public required string Code { get; init; }
    [Required, MaxLength(100)] public required string Name { get; init; }
    [Required, MaxLength(20)] public required string Unit { get; init; }
    [Range(typeof(decimal), "0", "9999999.999")] public decimal MinQuantity { get; init; }
    [Required] public DateOnly? ExpiryDate { get; init; }
    public string? RowVersion { get; init; }
}

/// <summary>Stock and expiry warnings with a concurrency token.</summary>
public sealed record IngredientResponse(string Id, string Code, string Name, string Unit, decimal Quantity,
    DateOnly? ExpiryDate, decimal MinQuantity, bool IsLowStock, bool IsOutOfStock, bool IsExpired,
    bool IsExpiringSoon, string RowVersion);

/// <summary>Import/export positive amount, signed adjustment delta or absolute stocktake amount.</summary>
public sealed record InventoryMovementRequest
{
    [Range(typeof(decimal), "-9999999.999", "9999999.999")] public decimal Quantity { get; init; }
    [Required, MaxLength(1000)] public required string Reason { get; init; }
    [Required] public required string RowVersion { get; init; }
}

/// <summary>Append-only inventory movement with before/after balances.</summary>
public sealed record InventoryTransactionResponse(string Id, string IngredientId, InventoryTransactionType Type,
    decimal Quantity, decimal PreviousQuantity, decimal ResultingQuantity, string Reason,
    string PerformedByUserId, DateTimeOffset CreatedAt);

/// <summary>Saved stock balance and its newly created movement.</summary>
public sealed record InventoryMovementResponse(IngredientResponse Ingredient, InventoryTransactionResponse Transaction);

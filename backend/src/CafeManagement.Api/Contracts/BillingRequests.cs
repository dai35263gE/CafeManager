using System.ComponentModel.DataAnnotations;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Contracts;

/// <summary>Promotion creation or replacement of its editable definition.</summary>
public sealed record SavePromotionRequest
{
    [Required, MaxLength(10), RegularExpression("^[A-Za-z0-9_-]+$")] public required string Code { get; init; }
    [Required, MaxLength(100)] public required string Name { get; init; }
    [Required] public DateTimeOffset StartAt { get; init; }
    [Required] public DateTimeOffset EndAt { get; init; }
    [EnumDataType(typeof(PromotionType))] public PromotionType Type { get; init; }
    [Range(typeof(decimal), "0.01", "999999999")] public decimal DiscountValue { get; init; }
    [Range(typeof(decimal), "0", "999999999")] public decimal? MaxDiscount { get; init; }
    [Range(typeof(decimal), "0", "999999999")] public decimal MinOrderAmount { get; init; }
    [EnumDataType(typeof(PromotionStatus))] public PromotionStatus Status { get; init; } = PromotionStatus.Active;
    [MaxLength(2000)] public string? ConditionDescription { get; init; }
}

/// <summary>Promotion definition; expired active definitions are reported as Expired.</summary>
public sealed record PromotionResponse(string Id, string Code, string Name, DateTimeOffset StartAt, DateTimeOffset EndAt,
    PromotionType Type, decimal DiscountValue, decimal? MaxDiscount, decimal MinOrderAmount,
    PromotionStatus Status, string? ConditionDescription, string EffectiveStatus);

public sealed record PromotionUsageSummaryResponse(int UsageCount, decimal TotalDiscountAmount);

/// <summary>Validate a code against an actual unpaid order, never a client-supplied total.</summary>
public sealed record ValidatePromotionRequest
{
    [Required, MaxLength(10)] public required string OrderId { get; init; }
    [Required, MaxLength(10)] public required string Code { get; init; }
}

/// <summary>Validated promotion preview; payment recalculates it inside its own transaction.</summary>
public sealed record PromotionValidationResponse(string PromotionId, string Code, decimal SubTotal,
    decimal VatAmount, decimal ServiceFeeAmount, decimal DiscountAmount, decimal TotalAmount,
    IReadOnlyList<string> Warnings);

/// <summary>Server-calculated invoice quote; payment revalidates the order token and totals.</summary>
public sealed record InvoiceQuoteRequest
{
    [Required, MaxLength(10)] public required string OrderId { get; init; }
    [Required] public required string RowVersion { get; init; }
    [MaxLength(10)] public string? PromotionCode { get; init; }
}

public sealed record InvoiceQuoteResponse(string OrderId, string RowVersion, string? PromotionId,
    string? PromotionCode, decimal SubTotal, decimal VatRatePercent, decimal VatAmount,
    decimal ServiceFeeRatePercent, decimal ServiceFeeAmount, decimal DiscountAmount,
    decimal TotalAmount, IReadOnlyList<string> Warnings);

/// <summary>Pay a served order once, with its current concurrency token.</summary>
public sealed record CreateInvoiceRequest
{
    [Required, MaxLength(10)] public required string OrderId { get; init; }
    [Required] public required string RowVersion { get; init; }
    [MaxLength(10)] public string? PromotionCode { get; init; }
    [EnumDataType(typeof(PaymentMethod))] public PaymentMethod PaymentMethod { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999.99")] public decimal ReceivedAmount { get; init; }
}

/// <summary>Manager-only invoice cancellation with a stale-write guard.</summary>
public sealed record CancelInvoiceRequest
{
    [Required] public required string RowVersion { get; init; }
    [Required, MaxLength(1000)] public required string Reason { get; init; }
}

/// <summary>Immutable invoice monetary snapshots and cancellation metadata.</summary>
public sealed record InvoiceResponse(string Id, string Code, string OrderId, string OrderCode, string? TableId,
    string? TableCode, string? PromotionId, string? PromotionCode, decimal SubTotal, decimal VatAmount,
    decimal ServiceFeeAmount, decimal DiscountAmount, decimal TotalAmount, PaymentMethod PaymentMethod,
    decimal ReceivedAmount, decimal ChangeAmount, string CashierId, DateTimeOffset PaidAt,
    InvoiceStatus Status, string? CancelReason, string? CancelledByUserId, DateTimeOffset? CancelledAt,
    string RowVersion, IReadOnlyList<OrderItemResponse> Items);

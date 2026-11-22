using System.ComponentModel.DataAnnotations;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Contracts;

/// <summary>A UTC reporting interval with inclusive From and exclusive To.</summary>
public sealed record ReportRange(DateTimeOffset From, DateTimeOffset To);

/// <summary>Daily net revenue and recorded discounts from paid invoices only.</summary>
public sealed record RevenueDayResponse(DateOnly Day, int InvoiceCount, decimal Revenue, decimal Discount);

/// <summary>Full-interval summary plus a page of daily revenue.</summary>
public sealed record RevenueReportResponse(DateTimeOffset From, DateTimeOffset To, int InvoiceCount,
    decimal TotalRevenue, decimal TotalDiscount, PageResponse<RevenueDayResponse> Days);

/// <summary>Sales quantities and gross line revenue including toppings, before invoice-level discounts.</summary>
public sealed record TopItemResponse(string MenuItemId, string Name, int Quantity, decimal GrossRevenue);

/// <summary>Net paid revenue grouped by payment method.</summary>
public sealed record PaymentMethodResponse(PaymentMethod PaymentMethod, int InvoiceCount, decimal Revenue);

/// <summary>Filters paid invoices belonging to one revenue, menu-item or payment-method report row.</summary>
public sealed record ReportDetailRequest
{
    [Required, MaxLength(30)] public required string Report { get; init; }
    [Required, MaxLength(150)] public required string Key { get; init; }
    [Required] public DateTimeOffset? From { get; init; }
    [Required] public DateTimeOffset? To { get; init; }
}

/// <summary>A paid invoice summary displayed beneath a report row.</summary>
public sealed record ReportInvoiceResponse(string Id, string Code, string? TableCode,
    DateTime PaidAt, decimal TotalAmount, PaymentMethod PaymentMethod);

/// <summary>A server-generated CSV download, not a JSON API response.</summary>
public sealed record CsvDownload(string FileName, byte[] Content);

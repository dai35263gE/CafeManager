using CafeManagement.Api.Contracts;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/bao-cao"), Authorize(Roles = Roles.Manager + "," + Roles.Warehouse)]
public sealed class ReportsController(IReportService service) : ApiController
{
    /// <summary>Full-range summary and paged daily net revenue from Paid invoices; [from,to) UTC.</summary>
    [HttpGet("revenue"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<RevenueReportResponse>>> Revenue(
        [FromQuery] ReportRange range, [FromQuery] ListRequest page, CancellationToken ct) =>
        Success(await service.RevenueAsync(range, page, ct));

    /// <summary>Rank sold items by quantity; gross line revenue includes toppings, before invoice discounts.</summary>
    [HttpGet("top-items"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<PageResponse<TopItemResponse>>>> TopItems(
        [FromQuery] ReportRange range, [FromQuery] ListRequest page, CancellationToken ct) =>
        Success(await service.TopItemsAsync(range, page, ct));

    /// <summary>Net Paid revenue and invoice counts by payment method.</summary>
    [HttpGet("payment-methods"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<PageResponse<PaymentMethodResponse>>>> Methods(
        [FromQuery] ReportRange range, [FromQuery] ListRequest page, CancellationToken ct) =>
        Success(await service.PaymentMethodsAsync(range, page, ct));

    /// <summary>Current ingredient balances and stock/expiry warnings, not historical stock valuation.</summary>
    [HttpGet("inventory")]
    public async Task<ActionResult<ApiResponse<PageResponse<IngredientResponse>>>> Inventory(
        [FromQuery] ListRequest page, string? filter, CancellationToken ct) =>
        Success(await service.InventoryAsync(page, filter, ct));

    /// <summary>List paid invoices represented by a selected revenue, menu-item or payment-method row.</summary>
    [HttpGet("details"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<PageResponse<ReportInvoiceResponse>>>> Details(
        [FromQuery] ReportDetailRequest request, [FromQuery] ListRequest page, CancellationToken ct) =>
        Success(await service.DetailsAsync(request, page, ct));

    /// <summary>Export all matching report rows, not only the first page, as UTF-8 CSV.</summary>
    [HttpGet("export/{report}")]
    public async Task<IActionResult> Export(string report, DateTimeOffset? from, DateTimeOffset? to,
        string? filter, string? search, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Warehouse) && !report.Equals("inventory", StringComparison.OrdinalIgnoreCase))
            throw new ApiException(403, "Warehouse can export only inventory reports.");
        var range = from.HasValue && to.HasValue ? new ReportRange(from.Value, to.Value) : null;
        var file = await service.ExportAsync(report, range, filter, search, ct);
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }
}

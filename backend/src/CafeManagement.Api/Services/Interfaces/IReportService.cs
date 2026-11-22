using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

public interface IReportService
{
    Task<RevenueReportResponse> RevenueAsync(ReportRange range, ListRequest page, CancellationToken ct);
    Task<PageResponse<TopItemResponse>> TopItemsAsync(ReportRange range, ListRequest page, CancellationToken ct);
    Task<PageResponse<PaymentMethodResponse>> PaymentMethodsAsync(ReportRange range, ListRequest page, CancellationToken ct);
    Task<PageResponse<IngredientResponse>> InventoryAsync(ListRequest page, string? filter, CancellationToken ct);
    Task<PageResponse<ReportInvoiceResponse>> DetailsAsync(
        ReportDetailRequest request, ListRequest page, CancellationToken ct);
    Task<CsvDownload> ExportAsync(string report, ReportRange? range, string? filter, string? search, CancellationToken ct);
}

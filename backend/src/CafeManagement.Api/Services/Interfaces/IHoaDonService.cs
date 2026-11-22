using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Services;

public interface IHoaDonService
{
    Task<PageResponse<InvoiceResponse>> TimKiemHoaDonAsync(ListRequest request, InvoiceStatus? status,
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);
    Task<InvoiceResponse> LayHoaDonAsync(string id, CancellationToken ct);
    Task<string> InHoaDonAsync(string id, CancellationToken ct);
    Task<InvoiceQuoteResponse> TaoBaoGiaAsync(InvoiceQuoteRequest request, CancellationToken ct);
    Task<InvoiceResponse> LapHoaDonAsync(CreateInvoiceRequest request, CancellationToken ct);
    Task<InvoiceResponse> HuyHoaDonAsync(string id, CancelInvoiceRequest request, CancellationToken ct);
}

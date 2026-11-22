using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Services;

public interface IKhuyenMaiService
{
    Task<PageResponse<PromotionResponse>> TimKiemKhuyenMaiAsync(ListRequest request, PromotionStatus? status, CancellationToken ct);
    Task<PromotionResponse> LayKhuyenMaiAsync(string id, CancellationToken ct);
    Task<PromotionResponse> ThemKhuyenMaiAsync(SavePromotionRequest request, CancellationToken ct);
    Task<PromotionResponse> SuaKhuyenMaiAsync(string id, SavePromotionRequest request, CancellationToken ct);
    Task TamNgungKhuyenMaiAsync(string id, CancellationToken ct);
    Task<PromotionResponse> KichHoatLaiKhuyenMaiAsync(string id, CancellationToken ct);
    Task<PromotionResponse> KetThucKhuyenMaiAsync(string id, CancellationToken ct);
    Task<PromotionValidationResponse> KiemTraHopLeAsync(ValidatePromotionRequest request, CancellationToken ct);
    Task<PageResponse<InvoiceResponse>> LayLichSuSuDungAsync(string id, ListRequest request, CancellationToken ct);
    Task<PromotionUsageSummaryResponse> LayThongKeSuDungAsync(string id, CancellationToken ct);
}

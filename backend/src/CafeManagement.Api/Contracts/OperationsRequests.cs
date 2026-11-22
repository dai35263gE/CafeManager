using System.ComponentModel.DataAnnotations;

namespace CafeManagement.Api.Contracts;

public sealed record SaveRecipeRequest
{
    [Required, MaxLength(10)] public required string MaMon { get; init; }
    [Required, MaxLength(10)] public required string MaNguyenLieu { get; init; }
    [Range(typeof(decimal), "0.001", "9999999.999")] public decimal SoLuongSuDung { get; init; }
}

public sealed record RecipeResponse(
    string MaDinhLuong, string MaMon, string TenMon,
    string MaNguyenLieu, string TenNguyenLieu, string DonViTinh, decimal SoLuongSuDung);

public sealed record AuditLogResponse(
    string MaNhatKy, string MaNhanVien, string TenNhanVien, string LoaiThaoTac,
    string DoiTuongTacDong, string NoiDung, DateTimeOffset ThoiGian);

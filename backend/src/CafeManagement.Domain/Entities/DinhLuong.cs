namespace CafeManagement.Domain.Entities;

public sealed class DinhLuong
{
    public string MaDinhLuong { get; set; } = string.Empty;
    public string MaMon { get; set; } = string.Empty;
    public Mon Mon { get; set; } = null!;
    public string MaNguyenLieu { get; set; } = string.Empty;
    public NguyenLieu NguyenLieu { get; set; } = null!;
    public decimal SoLuongSuDung { get; set; }
}

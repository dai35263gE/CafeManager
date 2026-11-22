using CafeManagement.Domain.Enums;

namespace CafeManagement.Domain.Entities;

public sealed class BienDongKho
{
    public string MaBienDong { get; set; } = string.Empty;
    public string MaNguyenLieu { get; set; } = string.Empty;
    public NguyenLieu NguyenLieu { get; set; } = null!;
    public string MaNhanVien { get; set; } = string.Empty;
    public NhanVien NhanVien { get; set; } = null!;
    public InventoryTransactionType LoaiBienDong { get; set; }
    public decimal SoLuong { get; set; }
    public DateTime ThoiGian { get; set; }
    public string LyDo { get; set; } = string.Empty;
}

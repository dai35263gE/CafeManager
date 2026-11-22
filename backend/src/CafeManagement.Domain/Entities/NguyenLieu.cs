namespace CafeManagement.Domain.Entities;

public sealed class NguyenLieu
{
    public string MaNguyenLieu { get; set; } = string.Empty;
    public string TenNguyenLieu { get; set; } = string.Empty;
    public string DonViTinh { get; set; } = string.Empty;
    public decimal SoLuongTon { get; set; }
    public decimal DinhMucToiThieu { get; set; }
    public DateOnly HanSuDung { get; set; } = DateOnly.MaxValue;
    public ICollection<BienDongKho> BienDongKhos { get; set; } = new List<BienDongKho>();
    public ICollection<DinhLuong> DinhLuongs { get; set; } = new List<DinhLuong>();
}

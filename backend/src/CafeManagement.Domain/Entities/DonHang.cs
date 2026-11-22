using CafeManagement.Domain.Enums;

namespace CafeManagement.Domain.Entities;

public sealed class DonHang
{
    public string MaDon { get; set; } = string.Empty;
    public string? MaBan { get; set; }
    public Ban? Ban { get; set; }
    public string MaNhanVien { get; set; } = string.Empty;
    public NhanVien NhanVien { get; set; } = null!;
    public OrderType LoaiDon { get; set; }
    public DateTime ThoiGianTao { get; set; }
    public OrderStatus TrangThai { get; set; } = OrderStatus.Open;
    public decimal TamTinh { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<ChiTietDonHang> ChiTiets { get; set; } = new List<ChiTietDonHang>();
    public ICollection<DonHangBan> DonHangBans { get; set; } = new List<DonHangBan>();
    public HoaDon? HoaDon { get; set; }
    public ICollection<ThongBao> ThongBaos { get; set; } = new List<ThongBao>();
}

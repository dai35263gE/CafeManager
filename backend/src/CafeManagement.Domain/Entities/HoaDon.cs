using CafeManagement.Domain.Enums;

namespace CafeManagement.Domain.Entities;

public sealed class HoaDon
{
    public string MaHoaDon { get; set; } = string.Empty;
    public string MaDon { get; set; } = string.Empty;
    public DonHang DonHang { get; set; } = null!;
    public string MaNhanVien { get; set; } = string.Empty;
    public NhanVien NhanVien { get; set; } = null!;
    public string? MaKhuyenMai { get; set; }
    public KhuyenMai? KhuyenMai { get; set; }
    public decimal TongTienHang { get; set; }
    public decimal ThueVAT { get; set; }
    public decimal PhiDichVu { get; set; }
    public decimal TienGiamGia { get; set; }
    public decimal TongThanhToan { get; set; }
    public PaymentMethod PhuongThucThanhToan { get; set; }
    public decimal TienKhachDua { get; set; }
    public decimal TienThua { get; set; }
    public DateTime ThoiGianLap { get; set; }
    public InvoiceStatus TrangThai { get; set; } = InvoiceStatus.Paid;
    public byte[] RowVersion { get; set; } = [];
}

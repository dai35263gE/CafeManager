using CafeManagement.Domain.Enums;

namespace CafeManagement.Domain.Entities;

public sealed class NhanVien
{
    public string MaNhanVien { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string ViTri { get; set; } = string.Empty;
    public string ThongTinLienHe { get; set; } = string.Empty;
    public EmployeeStatus TrangThai { get; set; } = EmployeeStatus.Working;
    public TaiKhoan? TaiKhoan { get; set; }
    public ICollection<DonHang> DonHangs { get; set; } = new List<DonHang>();
    public ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();
    public ICollection<NhatKyThaoTac> NhatKys { get; set; } = new List<NhatKyThaoTac>();
    public ICollection<BienDongKho> BienDongKhos { get; set; } = new List<BienDongKho>();
}

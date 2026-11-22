using CafeManagement.Domain.Common;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Domain.Entities;

public sealed class TaiKhoan
{
    public string TenDangNhap { get; set; } = string.Empty;
    public string MaNhanVien { get; set; } = string.Empty;
    public NhanVien NhanVien { get; set; } = null!;
    public string MaVaiTro { get; set; } = string.Empty;
    public VaiTro VaiTro { get; set; } = null!;
    public string MatKhauHash { get; set; } = string.Empty;
    public AccountStatus TrangThai { get; set; } = AccountStatus.Active;
    public DateTime ThoiGianDangNhap { get; set; } = AccountDefaults.NeverLoggedIn;
}

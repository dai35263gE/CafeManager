namespace CafeManagement.Domain.Entities;

public sealed class VaiTro
{
    public string MaVaiTro { get; set; } = string.Empty;
    public string TenVaiTro { get; set; } = string.Empty;
    public string DanhSachQuyen { get; set; } = string.Empty;
    public ICollection<TaiKhoan> TaiKhoans { get; set; } = new List<TaiKhoan>();
    public ICollection<ThongBao> ThongBaos { get; set; } = new List<ThongBao>();
}

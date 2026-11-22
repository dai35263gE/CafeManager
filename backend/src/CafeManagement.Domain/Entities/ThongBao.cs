namespace CafeManagement.Domain.Entities;

public sealed class ThongBao
{
    public string MaThongBao { get; set; } = string.Empty;
    public string? MaDon { get; set; }
    public DonHang? DonHang { get; set; }
    public string MaVaiTro { get; set; } = string.Empty;
    public VaiTro VaiTro { get; set; } = null!;
    public string NoiDung { get; set; } = string.Empty;
    public string LoaiThongBao { get; set; } = string.Empty;
    public DateTime ThoiGian { get; set; }
}

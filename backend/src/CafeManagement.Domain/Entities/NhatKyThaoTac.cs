namespace CafeManagement.Domain.Entities;

public sealed class NhatKyThaoTac
{
    public string MaNhatKy { get; set; } = string.Empty;
    public string MaNhanVien { get; set; } = string.Empty;
    public NhanVien NhanVien { get; set; } = null!;
    public string LoaiThaoTac { get; set; } = string.Empty;
    public string DoiTuongTacDong { get; set; } = string.Empty;
    public string NoiDung { get; set; } = string.Empty;
    public DateTime ThoiGian { get; set; }
}

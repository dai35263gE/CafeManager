namespace CafeManagement.Domain.Entities;

public sealed class DonHangBan
{
    public string MaDon { get; set; } = string.Empty;
    public DonHang DonHang { get; set; } = null!;
    public string MaBan { get; set; } = string.Empty;
    public Ban Ban { get; set; } = null!;
    public DateTime ThoiGianBatDau { get; set; }
    public DateTime? ThoiGianKetThuc { get; set; }
}

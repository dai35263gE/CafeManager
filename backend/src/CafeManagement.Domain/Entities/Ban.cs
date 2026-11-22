using CafeManagement.Domain.Enums;

namespace CafeManagement.Domain.Entities;

public sealed class Ban
{
    public string MaBan { get; set; } = string.Empty;
    public string KhuVuc { get; set; } = string.Empty;
    public TableStatus TrangThai { get; set; } = TableStatus.Available;
    public string GhiChu { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public ICollection<DonHang> DonHangs { get; set; } = new List<DonHang>();
    public ICollection<DonHangBan> DonHangBans { get; set; } = new List<DonHangBan>();
}

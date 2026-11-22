using CafeManagement.Domain.Enums;

namespace CafeManagement.Domain.Entities;

public sealed class ChiTietDonHang
{
    public string MaChiTiet { get; set; } = string.Empty;
    public string MaDon { get; set; } = string.Empty;
    public DonHang DonHang { get; set; } = null!;
    public string MaMon { get; set; } = string.Empty;
    public Mon Mon { get; set; } = null!;
    public string? MaKichCo { get; set; }
    public KichCo? KichCo { get; set; }
    public int SoLuong { get; set; } = 1;
    public decimal DonGia { get; set; }
    public string GhiChu { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public OrderItemStatus TrangThaiMon { get; set; } = OrderItemStatus.Waiting;
    public ICollection<ChiTietTopping> ChiTietToppings { get; set; } = new List<ChiTietTopping>();
}

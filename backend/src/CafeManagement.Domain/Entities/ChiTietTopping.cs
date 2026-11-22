namespace CafeManagement.Domain.Entities;

public sealed class ChiTietTopping
{
    public string MaChiTiet { get; set; } = string.Empty;
    public ChiTietDonHang ChiTiet { get; set; } = null!;
    public string MaTopping { get; set; } = string.Empty;
    public Topping Topping { get; set; } = null!;
}

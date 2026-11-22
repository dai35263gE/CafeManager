using CafeManagement.Domain.Enums;

namespace CafeManagement.Domain.Entities;

public sealed class Mon
{
    public string MaMon { get; set; } = string.Empty;
    public string MaDanhMuc { get; set; } = string.Empty;
    public DanhMuc DanhMuc { get; set; } = null!;
    public string TenMon { get; set; } = string.Empty;
    public string MoTa { get; set; } = string.Empty;
    public string HinhAnh { get; set; } = string.Empty;
    public MenuItemStatus TrangThai { get; set; } = MenuItemStatus.Available;
    public ICollection<KichCo> KichCos { get; set; } = new List<KichCo>();
    public ICollection<Topping> Toppings { get; set; } = new List<Topping>();
    public ICollection<DinhLuong> DinhLuongs { get; set; } = new List<DinhLuong>();
}

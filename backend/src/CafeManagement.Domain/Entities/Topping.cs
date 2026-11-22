namespace CafeManagement.Domain.Entities;

public sealed class Topping
{
    public string MaTopping { get; set; } = string.Empty;
    public string MaMon { get; set; } = string.Empty;
    public Mon Mon { get; set; } = null!;
    public string TenTopping { get; set; } = string.Empty;
    public decimal GiaBan { get; set; }
}

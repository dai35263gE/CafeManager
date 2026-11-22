namespace CafeManagement.Domain.Entities;

public sealed class KichCo
{
    public string MaKichCo { get; set; } = string.Empty;
    public string MaMon { get; set; } = string.Empty;
    public Mon Mon { get; set; } = null!;
    public string TenKichCo { get; set; } = string.Empty;
    public decimal GiaBan { get; set; }
}

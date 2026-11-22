namespace CafeManagement.Domain.Entities;

public sealed class DanhMuc
{
    public string MaDanhMuc { get; set; } = string.Empty;
    public string TenDanhMuc { get; set; } = string.Empty;
    public ICollection<Mon> Mons { get; set; } = new List<Mon>();
}

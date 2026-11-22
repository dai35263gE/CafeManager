using CafeManagement.Domain.Enums;

namespace CafeManagement.Domain.Entities;

public sealed class KhuyenMai
{
    public string MaKhuyenMai { get; set; } = string.Empty;
    public string TenChuongTrinh { get; set; } = string.Empty;
    public PromotionType LoaiGiamGia { get; set; }
    public decimal GiaTriGiam { get; set; }
    public DateOnly NgayBatDau { get; set; }
    public DateOnly NgayKetThuc { get; set; }
    public decimal GiaTriDonToiThieu { get; set; }
    public PromotionStatus TrangThai { get; set; } = PromotionStatus.Active;
    public ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();
}

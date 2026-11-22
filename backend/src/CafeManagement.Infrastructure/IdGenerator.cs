using System.Security.Cryptography;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Infrastructure;

/// <summary>Generates the varchar(10) primary keys: a short prefix plus random characters.</summary>
public sealed class IdGenerator(CafeDbContext db)
{
    public const int MaxLength = 10;
    private const string Alphabet = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private readonly HashSet<string> issued = [];

    public Task<string> NhanVienAsync(CancellationToken ct = default) => NextAsync("NV", ct);
    public Task<string> VaiTroAsync(CancellationToken ct = default) => NextAsync("VT", ct);
    public Task<string> NhatKyAsync(CancellationToken ct = default) => NextAsync("NK", ct);
    public Task<string> ThongBaoAsync(CancellationToken ct = default) => NextAsync("TB", ct);
    public Task<string> BanAsync(CancellationToken ct = default) => NextAsync("BAN", ct);
    public Task<string> DonHangAsync(CancellationToken ct = default) => NextAsync("DH", ct);
    public Task<string> ChiTietAsync(CancellationToken ct = default) => NextAsync("CT", ct);
    public Task<string> HoaDonAsync(CancellationToken ct = default) => NextAsync("HD", ct);
    public Task<string> KhuyenMaiAsync(CancellationToken ct = default) => NextAsync("KM", ct);
    public Task<string> DanhMucAsync(CancellationToken ct = default) => NextAsync("DM", ct);
    public Task<string> MonAsync(CancellationToken ct = default) => NextAsync("MON", ct);
    public Task<string> KichCoAsync(CancellationToken ct = default) => NextAsync("KC", ct);
    public Task<string> ToppingAsync(CancellationToken ct = default) => NextAsync("TP", ct);
    public Task<string> DinhLuongAsync(CancellationToken ct = default) => NextAsync("DL", ct);
    public Task<string> NguyenLieuAsync(CancellationToken ct = default) => NextAsync("NL", ct);
    public Task<string> BienDongAsync(CancellationToken ct = default) => NextAsync("BD", ct);

    public async Task<string> NextAsync(string prefix, CancellationToken ct = default)
    {
        var length = MaxLength - prefix.Length;
        while (true)
        {
            var id = prefix + string.Create(length, 0, static (span, _) =>
            {
                for (var i = 0; i < span.Length; i++)
                    span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            });
            if (!issued.Contains(id) && !await ExistsAsync(prefix, id, ct))
            {
                issued.Add(id);
                return id;
            }
        }
    }

    private Task<bool> ExistsAsync(string prefix, string id, CancellationToken ct) => prefix switch
    {
        "NV" => db.NhanViens.AnyAsync(x => x.MaNhanVien == id, ct),
        "VT" => db.VaiTros.AnyAsync(x => x.MaVaiTro == id, ct),
        "NK" => db.NhatKyThaoTacs.AnyAsync(x => x.MaNhatKy == id, ct),
        "TB" => db.ThongBaos.AnyAsync(x => x.MaThongBao == id, ct),
        "BAN" => db.Bans.AnyAsync(x => x.MaBan == id, ct),
        "DH" => db.DonHangs.AnyAsync(x => x.MaDon == id, ct),
        "CT" => db.ChiTietDonHangs.AnyAsync(x => x.MaChiTiet == id, ct),
        "HD" => db.HoaDons.AnyAsync(x => x.MaHoaDon == id, ct),
        "KM" => db.KhuyenMais.AnyAsync(x => x.MaKhuyenMai == id, ct),
        "DM" => db.DanhMucs.AnyAsync(x => x.MaDanhMuc == id, ct),
        "MON" => db.Mons.AnyAsync(x => x.MaMon == id, ct),
        "KC" => db.KichCos.AnyAsync(x => x.MaKichCo == id, ct),
        "TP" => db.Toppings.AnyAsync(x => x.MaTopping == id, ct),
        "DL" => db.DinhLuongs.AnyAsync(x => x.MaDinhLuong == id, ct),
        "NL" => db.NguyenLieus.AnyAsync(x => x.MaNguyenLieu == id, ct),
        "BD" => db.BienDongKhos.AnyAsync(x => x.MaBienDong == id, ct),
        _ => throw new ArgumentException("Unknown id prefix.", nameof(prefix))
    };
}

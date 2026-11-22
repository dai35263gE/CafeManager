using System.Data;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CafeManagement.Infrastructure.Seeding;

public sealed class DevelopmentSeeder(
    CafeDbContext db,
    IdGenerator ids,
    IPasswordHasher<TaiKhoan> passwordHasher,
    IConfiguration configuration,
    ILogger<DevelopmentSeeder> logger)
{
    private static readonly IReadOnlyDictionary<string, string> Positions = new Dictionary<string, string>
    {
        [Roles.Admin] = "Quản trị hệ thống",
        [Roles.Manager] = "Quản lý",
        [Roles.Waiter] = "Phục vụ",
        [Roles.Barista] = "Pha chế",
        [Roles.Cashier] = "Thu ngân",
        [Roles.Warehouse] = "Thủ kho"
    };

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var passwords = Roles.All.ToDictionary(
            role => role.ToLowerInvariant(),
            role => configuration[$"Seed:Passwords:{role.ToLowerInvariant()}"]);
        if (passwords.Any(x => string.IsNullOrWhiteSpace(x.Value)))
            throw new InvalidOperationException("Configure all six Seed:Passwords values before enabling Development seed.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        foreach (var role in Roles.All)
        {
            var (id, permissions) = Roles.Definitions[role];
            if (!await db.VaiTros.AnyAsync(x => x.MaVaiTro == id, cancellationToken))
                db.VaiTros.Add(new VaiTro { MaVaiTro = id, TenVaiTro = role, DanhSachQuyen = permissions });
        }

        for (var index = 0; index < Roles.All.Count; index++)
        {
            var role = Roles.All[index];
            var username = role.ToLowerInvariant();
            if (await db.TaiKhoans.AnyAsync(x => x.TenDangNhap == username, cancellationToken))
                continue;

            var employeeId = $"NV{index + 1:00}";
            var (roleId, _) = Roles.Definitions[role];
            var employee = await db.NhanViens.SingleOrDefaultAsync(x => x.MaNhanVien == employeeId, cancellationToken);
            employee ??= new NhanVien
            {
                MaNhanVien = employeeId,
                HoTen = $"Demo {role}",
                ViTri = Positions[role],
                ThongTinLienHe = $"{username}@cafe.example",
                TrangThai = EmployeeStatus.Working
            };
            if (db.Entry(employee).State == EntityState.Detached)
                db.NhanViens.Add(employee);

            var account = new TaiKhoan
            {
                TenDangNhap = username,
                MaNhanVien = employeeId,
                NhanVien = employee,
                MaVaiTro = roleId,
                TrangThai = AccountStatus.Active,
                ThoiGianDangNhap = AccountDefaults.NeverLoggedIn
            };
            account.MatKhauHash = passwordHasher.HashPassword(account, passwords[username]!);
            db.TaiKhoans.Add(account);
        }

        for (var index = 1; index <= 10; index++)
        {
            var code = $"BAN{index:00}";
            if (!await db.Bans.AnyAsync(x => x.MaBan == code, cancellationToken))
                db.Bans.Add(new Ban { MaBan = code, KhuVuc = index <= 5 ? "Tầng trệt" : "Sân vườn" });
        }

        var categories = new Dictionary<string, DanhMuc>();
        foreach (var name in new[] { "Cà phê", "Trà", "Nước ép", "Bánh" })
        {
            var category = await db.DanhMucs.SingleOrDefaultAsync(x => x.TenDanhMuc == name, cancellationToken);
            if (category is null)
            {
                category = new DanhMuc { MaDanhMuc = $"DM{categories.Count + 1:00}", TenDanhMuc = name };
                db.DanhMucs.Add(category);
            }
            categories.Add(name, category);
        }

        var ingredients = new Dictionary<string, NguyenLieu>();
        foreach (var item in new[]
        {
            (Id: "NL01", Name: "Hạt cà phê", Unit: "kg", Quantity: 10m, Min: 2m),
            (Id: "NL02", Name: "Sữa tươi", Unit: "lít", Quantity: 5m, Min: 1m),
            (Id: "NL03", Name: "Trà", Unit: "kg", Quantity: 3m, Min: 1m),
            (Id: "NL04", Name: "Cam", Unit: "kg", Quantity: 4m, Min: 1m)
        })
        {
            var ingredient = await db.NguyenLieus.SingleOrDefaultAsync(x => x.MaNguyenLieu == item.Id, cancellationToken);
            if (ingredient is null)
            {
                var now = DateTime.UtcNow;
                ingredient = new NguyenLieu
                {
                    MaNguyenLieu = item.Id,
                    TenNguyenLieu = item.Name,
                    DonViTinh = item.Unit,
                    SoLuongTon = item.Quantity,
                    DinhMucToiThieu = item.Min,
                    HanSuDung = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6)
                };
                db.BienDongKhos.Add(new BienDongKho
                {
                    MaBienDong = await ids.BienDongAsync(cancellationToken),
                    MaNguyenLieu = ingredient.MaNguyenLieu,
                    NguyenLieu = ingredient,
                    MaNhanVien = "NV06",
                    LoaiBienDong = InventoryTransactionType.Import,
                    SoLuong = item.Quantity,
                    ThoiGian = now,
                    LyDo = "Development opening stock"
                });
                db.NguyenLieus.Add(ingredient);
            }
            ingredients.Add(item.Name, ingredient);
        }

        var menu = new[]
        {
            (Id: "MON01", Name: "Cà phê đen", Category: "Cà phê", Ingredient: "Hạt cà phê"),
            (Id: "MON02", Name: "Cà phê sữa", Category: "Cà phê", Ingredient: "Sữa tươi"),
            (Id: "MON03", Name: "Trà đào", Category: "Trà", Ingredient: "Trà"),
            (Id: "MON04", Name: "Trà sữa", Category: "Trà", Ingredient: "Sữa tươi"),
            (Id: "MON05", Name: "Nước cam", Category: "Nước ép", Ingredient: "Cam"),
            (Id: "MON06", Name: "Bánh croissant", Category: "Bánh", Ingredient: "Hạt cà phê")
        };
        var recipeIndex = 0;
        foreach (var entry in menu)
        {
            if (await db.Mons.AnyAsync(x => x.MaMon == entry.Id, cancellationToken))
                continue;
            var dish = new Mon
            {
                MaMon = entry.Id,
                MaDanhMuc = categories[entry.Category].MaDanhMuc,
                DanhMuc = categories[entry.Category],
                TenMon = entry.Name,
                TrangThai = MenuItemStatus.Available,
                KichCos =
                [
                    new KichCo { MaKichCo = $"KC{entry.Id.Substring(3)}1", TenKichCo = "M", GiaBan = 30000m },
                    new KichCo { MaKichCo = $"KC{entry.Id.Substring(3)}2", TenKichCo = "L", GiaBan = 40000m }
                ],
                Toppings =
                [
                    new Topping { MaTopping = $"TP{entry.Id.Substring(3)}1", TenTopping = "Trân châu", GiaBan = 5000m },
                    new Topping { MaTopping = $"TP{entry.Id.Substring(3)}2", TenTopping = "Kem sữa", GiaBan = 7000m }
                ]
            };
            var recipeId = $"DL{++recipeIndex:02}";
            dish.DinhLuongs.Add(new DinhLuong
            {
                MaDinhLuong = recipeId,
                MaMon = dish.MaMon,
                MaNguyenLieu = ingredients[entry.Ingredient].MaNguyenLieu,
                NguyenLieu = ingredients[entry.Ingredient],
                SoLuongSuDung = 0.02m
            });
            db.Mons.Add(dish);
        }

        if (!await db.KhuyenMais.AnyAsync(cancellationToken))
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            db.KhuyenMais.Add(new KhuyenMai
            {
                MaKhuyenMai = "KM01",
                TenChuongTrinh = "Giảm 10%",
                LoaiGiamGia = PromotionType.Percentage,
                GiaTriGiam = 10,
                GiaTriDonToiThieu = 50000,
                NgayBatDau = today.AddDays(-1),
                NgayKetThuc = today.AddMonths(3),
                TrangThai = PromotionStatus.Active
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Development seed completed for the ERD model.");
    }
}

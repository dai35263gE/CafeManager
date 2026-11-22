using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeManagement.Infrastructure.Persistence;

public sealed class CafeDbContext(DbContextOptions<CafeDbContext> options) : DbContext(options)
{
    public DbSet<NhanVien> NhanViens => Set<NhanVien>();
    public DbSet<TaiKhoan> TaiKhoans => Set<TaiKhoan>();
    public DbSet<VaiTro> VaiTros => Set<VaiTro>();
    public DbSet<NhatKyThaoTac> NhatKyThaoTacs => Set<NhatKyThaoTac>();
    public DbSet<ThongBao> ThongBaos => Set<ThongBao>();
    public DbSet<Ban> Bans => Set<Ban>();
    public DbSet<DonHang> DonHangs => Set<DonHang>();
    public DbSet<DonHangBan> DonHangBans => Set<DonHangBan>();
    public DbSet<ChiTietDonHang> ChiTietDonHangs => Set<ChiTietDonHang>();
    public DbSet<ChiTietTopping> ChiTietToppings => Set<ChiTietTopping>();
    public DbSet<HoaDon> HoaDons => Set<HoaDon>();
    public DbSet<KhuyenMai> KhuyenMais => Set<KhuyenMai>();
    public DbSet<DanhMuc> DanhMucs => Set<DanhMuc>();
    public DbSet<Mon> Mons => Set<Mon>();
    public DbSet<KichCo> KichCos => Set<KichCo>();
    public DbSet<Topping> Toppings => Set<Topping>();
    public DbSet<DinhLuong> DinhLuongs => Set<DinhLuong>();
    public DbSet<NguyenLieu> NguyenLieus => Set<NguyenLieu>();
    public DbSet<BienDongKho> BienDongKhos => Set<BienDongKho>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder) =>
        builder.Properties<string>().AreUnicode(false);

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema("dbo");

        model.Entity<NhanVien>(e =>
        {
            e.ToTable("NhanVien", table =>
                table.HasCheckConstraint("CK_NhanVien_trang_thai", "[trang_thai] IN ('Working', 'Resigned')"));
            e.HasKey(x => x.MaNhanVien);
            e.Property(x => x.MaNhanVien).HasColumnName("ma_nhan_vien").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.HoTen).HasColumnName("ho_ten").HasMaxLength(100).IsUnicode().IsRequired();
            e.Property(x => x.ViTri).HasColumnName("vi_tri").HasMaxLength(50).IsUnicode().IsRequired();
            e.Property(x => x.ThongTinLienHe).HasColumnName("thong_tin_lien_he").HasMaxLength(150).IsUnicode().IsRequired();
            EnumProperty(e.Property(x => x.TrangThai), "trang_thai", 24);
            e.HasMany(x => x.DonHangs).WithOne(x => x.NhanVien).HasForeignKey(x => x.MaNhanVien).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.HoaDons).WithOne(x => x.NhanVien).HasForeignKey(x => x.MaNhanVien).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.NhatKys).WithOne(x => x.NhanVien).HasForeignKey(x => x.MaNhanVien).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.BienDongKhos).WithOne(x => x.NhanVien).HasForeignKey(x => x.MaNhanVien).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<TaiKhoan>(e =>
        {
            e.ToTable("TaiKhoan", table =>
                table.HasCheckConstraint("CK_TaiKhoan_trang_thai", "[trang_thai] IN ('Active', 'Locked', 'Disabled')"));
            e.HasKey(x => x.TenDangNhap);
            e.Property(x => x.TenDangNhap).HasColumnName("ten_dang_nhap").HasMaxLength(50).ValueGeneratedNever();
            e.Property(x => x.MaNhanVien).HasColumnName("ma_nhan_vien").HasMaxLength(10).IsRequired();
            e.Property(x => x.MaVaiTro).HasColumnName("ma_vai_tro").HasMaxLength(10).IsRequired();
            e.Property(x => x.MatKhauHash).HasColumnName("mat_khau_hash").HasMaxLength(255).IsRequired();
            EnumProperty(e.Property(x => x.TrangThai), "trang_thai", 24);
            e.Property(x => x.ThoiGianDangNhap).HasColumnName("thoi_gian_dang_nhap").HasColumnType("datetime").IsRequired();
            e.HasIndex(x => x.MaNhanVien).IsUnique();
            e.HasOne(x => x.NhanVien).WithOne(x => x.TaiKhoan).HasForeignKey<TaiKhoan>(x => x.MaNhanVien).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.VaiTro).WithMany(x => x.TaiKhoans).HasForeignKey(x => x.MaVaiTro).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<VaiTro>(e =>
        {
            e.ToTable("VaiTro");
            e.HasKey(x => x.MaVaiTro);
            e.Property(x => x.MaVaiTro).HasColumnName("ma_vai_tro").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.TenVaiTro).HasColumnName("ten_vai_tro").HasMaxLength(50).IsUnicode().IsRequired();
            e.Property(x => x.DanhSachQuyen).HasColumnName("danh_sach_quyen").HasColumnType("varchar(max)").IsRequired();
        });

        model.Entity<NhatKyThaoTac>(e =>
        {
            e.ToTable("NhatKyThaoTac");
            e.HasKey(x => x.MaNhatKy);
            e.Property(x => x.MaNhatKy).HasColumnName("ma_nhat_ky").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.MaNhanVien).HasColumnName("ma_nhan_vien").HasMaxLength(10).IsRequired();
            e.Property(x => x.LoaiThaoTac).HasColumnName("loai_thao_tac").HasMaxLength(50).IsRequired();
            e.Property(x => x.DoiTuongTacDong).HasColumnName("doi_tuong_tac_dong").HasMaxLength(100).IsRequired();
            e.Property(x => x.NoiDung).HasColumnName("noi_dung").HasColumnType("nvarchar(max)").IsUnicode().IsRequired();
            e.Property(x => x.ThoiGian).HasColumnName("thoi_gian").HasColumnType("datetime").IsRequired();
        });

        model.Entity<ThongBao>(e =>
        {
            e.ToTable("ThongBao");
            e.HasKey(x => x.MaThongBao);
            e.Property(x => x.MaThongBao).HasColumnName("ma_thong_bao").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.MaDon).HasColumnName("ma_don").HasMaxLength(10);
            e.Property(x => x.MaVaiTro).HasColumnName("ma_vai_tro").HasMaxLength(10).IsRequired();
            e.Property(x => x.NoiDung).HasColumnName("noi_dung").HasColumnType("nvarchar(max)").IsUnicode().IsRequired();
            e.Property(x => x.LoaiThongBao).HasColumnName("loai_thong_bao").HasMaxLength(50).IsRequired();
            e.Property(x => x.ThoiGian).HasColumnName("thoi_gian").HasColumnType("datetime").IsRequired();
            e.HasOne(x => x.DonHang).WithMany(x => x.ThongBaos).HasForeignKey(x => x.MaDon).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.VaiTro).WithMany(x => x.ThongBaos).HasForeignKey(x => x.MaVaiTro).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Ban>(e =>
        {
            e.ToTable("Ban", table =>
                table.HasCheckConstraint("CK_Ban_trang_thai", "[trang_thai] IN ('Available', 'InUse', 'NeedsCleaning', 'Disabled')"));
            e.HasKey(x => x.MaBan);
            e.Property(x => x.MaBan).HasColumnName("ma_ban").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.KhuVuc).HasColumnName("khu_vuc").HasMaxLength(50).IsUnicode().IsRequired();
            EnumProperty(e.Property(x => x.TrangThai), "trang_thai", 24);
            e.Property(x => x.GhiChu).HasColumnName("ghi_chu").HasMaxLength(255).IsUnicode().IsRequired();
            e.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
        });

        model.Entity<DonHang>(e =>
        {
            e.ToTable("DonHang", table =>
            {
                table.HasCheckConstraint("CK_DonHang_loai_don", "[loai_don] IN ('DineIn', 'TakeAway')");
                table.HasCheckConstraint("CK_DonHang_trang_thai", "[trang_thai] IN ('Open', 'Confirmed', 'Preparing', 'Served', 'Paid', 'Cancelled')");
            });
            e.HasKey(x => x.MaDon);
            e.Property(x => x.MaDon).HasColumnName("ma_don").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.MaBan).HasColumnName("ma_ban").HasMaxLength(10);
            e.Property(x => x.MaNhanVien).HasColumnName("ma_nhan_vien").HasMaxLength(10).IsRequired();
            EnumProperty(e.Property(x => x.LoaiDon), "loai_don", 24);
            e.Property(x => x.ThoiGianTao).HasColumnName("thoi_gian_tao").HasColumnType("datetime").IsRequired();
            EnumProperty(e.Property(x => x.TrangThai), "trang_thai", 24);
            e.Property(x => x.TamTinh).HasColumnName("tam_tinh").HasPrecision(12, 2).IsRequired();
            e.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
            e.HasOne(x => x.Ban).WithMany(x => x.DonHangs).HasForeignKey(x => x.MaBan).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.DonHangBans).WithOne(x => x.DonHang).HasForeignKey(x => x.MaDon).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.ChiTiets).WithOne(x => x.DonHang).HasForeignKey(x => x.MaDon).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.HoaDon).WithOne(x => x.DonHang).HasForeignKey<HoaDon>(x => x.MaDon).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<DonHangBan>(e =>
        {
            e.ToTable("DonHangBan");
            e.HasKey(x => new { x.MaDon, x.MaBan, x.ThoiGianBatDau });
            e.Property(x => x.MaDon).HasColumnName("ma_don").HasMaxLength(10);
            e.Property(x => x.MaBan).HasColumnName("ma_ban").HasMaxLength(10);
            e.Property(x => x.ThoiGianBatDau).HasColumnName("thoi_gian_bat_dau").HasColumnType("datetime2(7)");
            e.Property(x => x.ThoiGianKetThuc).HasColumnName("thoi_gian_ket_thuc").HasColumnType("datetime2(7)");
            e.HasIndex(x => x.MaBan).IsUnique().HasFilter("[thoi_gian_ket_thuc] IS NULL");
            e.HasIndex(x => new { x.MaDon, x.ThoiGianKetThuc });
            e.HasOne(x => x.Ban).WithMany(x => x.DonHangBans).HasForeignKey(x => x.MaBan).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ChiTietDonHang>(e =>
        {
            e.ToTable("ChiTietDonHang", table =>
                table.HasCheckConstraint("CK_ChiTietDonHang_trang_thai_mon", "[trang_thai_mon] IN ('Waiting', 'Preparing', 'Completed', 'Served', 'Cancelled')"));
            e.HasKey(x => x.MaChiTiet);
            e.Property(x => x.MaChiTiet).HasColumnName("ma_chi_tiet").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.MaDon).HasColumnName("ma_don").HasMaxLength(10).IsRequired();
            e.Property(x => x.MaMon).HasColumnName("ma_mon").HasMaxLength(10).IsRequired();
            e.Property(x => x.MaKichCo).HasColumnName("ma_kich_co").HasMaxLength(10);
            e.Property(x => x.SoLuong).HasColumnName("so_luong").IsRequired();
            e.Property(x => x.DonGia).HasColumnName("don_gia").HasPrecision(12, 2).IsRequired();
            e.Property(x => x.GhiChu).HasColumnName("ghi_chu").HasMaxLength(255).IsUnicode().IsRequired();
            e.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
            EnumProperty(e.Property(x => x.TrangThaiMon), "trang_thai_mon", 24);
            e.HasOne(x => x.Mon).WithMany().HasForeignKey(x => x.MaMon).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.KichCo).WithMany().HasForeignKey(x => x.MaKichCo).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ChiTietTopping>(e =>
        {
            e.ToTable("ChiTietTopping");
            e.HasKey(x => new { x.MaChiTiet, x.MaTopping });
            e.Property(x => x.MaChiTiet).HasColumnName("ma_chi_tiet").HasMaxLength(10);
            e.Property(x => x.MaTopping).HasColumnName("ma_topping").HasMaxLength(10);
            e.HasOne(x => x.ChiTiet).WithMany(x => x.ChiTietToppings).HasForeignKey(x => x.MaChiTiet).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Topping).WithMany().HasForeignKey(x => x.MaTopping).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<HoaDon>(e =>
        {
            e.ToTable("HoaDon", table =>
            {
                table.HasCheckConstraint("CK_HoaDon_phuong_thuc_thanh_toan", "[phuong_thuc_thanh_toan] IN ('Cash', 'BankTransfer', 'Card', 'EWallet')");
                table.HasCheckConstraint("CK_HoaDon_trang_thai", "[trang_thai] IN ('Paid', 'Cancelled')");
            });
            e.HasKey(x => x.MaHoaDon);
            e.Property(x => x.MaHoaDon).HasColumnName("ma_hoa_don").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.MaDon).HasColumnName("ma_don").HasMaxLength(10).IsRequired();
            e.Property(x => x.MaNhanVien).HasColumnName("ma_nhan_vien").HasMaxLength(10).IsRequired();
            e.Property(x => x.MaKhuyenMai).HasColumnName("ma_khuyen_mai").HasMaxLength(10);
            e.HasIndex(x => x.MaDon).IsUnique();
            Money(e.Property(x => x.TongTienHang), "tong_tien_hang");
            Money(e.Property(x => x.ThueVAT), "thue_vat");
            Money(e.Property(x => x.PhiDichVu), "phi_dich_vu");
            Money(e.Property(x => x.TienGiamGia), "tien_giam_gia");
            Money(e.Property(x => x.TongThanhToan), "tong_thanh_toan");
            EnumProperty(e.Property(x => x.PhuongThucThanhToan), "phuong_thuc_thanh_toan", 24);
            Money(e.Property(x => x.TienKhachDua), "tien_khach_dua");
            Money(e.Property(x => x.TienThua), "tien_thua");
            e.Property(x => x.ThoiGianLap).HasColumnName("thoi_gian_lap").HasColumnType("datetime").IsRequired();
            e.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
            EnumProperty(e.Property(x => x.TrangThai), "trang_thai", 24);
            e.HasOne(x => x.KhuyenMai).WithMany(x => x.HoaDons).HasForeignKey(x => x.MaKhuyenMai).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<KhuyenMai>(e =>
        {
            e.ToTable("KhuyenMai", table =>
            {
                table.HasCheckConstraint("CK_KhuyenMai_loai_giam_gia", "[loai_giam_gia] IN ('Percentage', 'FixedAmount')");
                table.HasCheckConstraint("CK_KhuyenMai_trang_thai", "[trang_thai] IN ('Active', 'Inactive', 'Expired')");
            });
            e.HasKey(x => x.MaKhuyenMai);
            e.Property(x => x.MaKhuyenMai).HasColumnName("ma_khuyen_mai").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.TenChuongTrinh).HasColumnName("ten_chuong_trinh").HasMaxLength(100).IsUnicode().IsRequired();
            EnumProperty(e.Property(x => x.LoaiGiamGia), "loai_giam_gia", 24);
            Money(e.Property(x => x.GiaTriGiam), "gia_tri_giam");
            e.Property(x => x.NgayBatDau).HasColumnName("ngay_bat_dau").HasColumnType("date").IsRequired();
            e.Property(x => x.NgayKetThuc).HasColumnName("ngay_ket_thuc").HasColumnType("date").IsRequired();
            Money(e.Property(x => x.GiaTriDonToiThieu), "gia_tri_don_toi_thieu");
            EnumProperty(e.Property(x => x.TrangThai), "trang_thai", 24);
        });

        model.Entity<DanhMuc>(e =>
        {
            e.ToTable("DanhMuc");
            e.HasKey(x => x.MaDanhMuc);
            e.Property(x => x.MaDanhMuc).HasColumnName("ma_danh_muc").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.TenDanhMuc).HasColumnName("ten_danh_muc").HasMaxLength(100).IsUnicode().IsRequired();
        });

        model.Entity<Mon>(e =>
        {
            e.ToTable("Mon", table =>
                table.HasCheckConstraint("CK_Mon_trang_thai", "[trang_thai] IN ('Available', 'OutOfStock', 'Discontinued')"));
            e.HasKey(x => x.MaMon);
            e.Property(x => x.MaMon).HasColumnName("ma_mon").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.MaDanhMuc).HasColumnName("ma_danh_muc").HasMaxLength(10).IsRequired();
            e.Property(x => x.TenMon).HasColumnName("ten_mon").HasMaxLength(100).IsUnicode().IsRequired();
            e.Property(x => x.MoTa).HasColumnName("mo_ta").HasColumnType("nvarchar(max)").IsUnicode().IsRequired();
            e.Property(x => x.HinhAnh).HasColumnName("hinh_anh").HasMaxLength(255).IsRequired();
            EnumProperty(e.Property(x => x.TrangThai), "trang_thai", 24);
            e.Property<byte[]>("RowVersion").HasColumnName("row_version").IsRequired().IsRowVersion();
            e.HasOne(x => x.DanhMuc).WithMany(x => x.Mons).HasForeignKey(x => x.MaDanhMuc).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.KichCos).WithOne(x => x.Mon).HasForeignKey(x => x.MaMon).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Toppings).WithOne(x => x.Mon).HasForeignKey(x => x.MaMon).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.DinhLuongs).WithOne(x => x.Mon).HasForeignKey(x => x.MaMon).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<KichCo>(e =>
        {
            e.ToTable("KichCo");
            e.HasKey(x => x.MaKichCo);
            e.Property(x => x.MaKichCo).HasColumnName("ma_kich_co").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.MaMon).HasColumnName("ma_mon").HasMaxLength(10).IsRequired();
            e.Property(x => x.TenKichCo).HasColumnName("ten_kich_co").HasMaxLength(20).IsUnicode().IsRequired();
            Money(e.Property(x => x.GiaBan), "gia_ban");
        });

        model.Entity<Topping>(e =>
        {
            e.ToTable("Topping");
            e.HasKey(x => x.MaTopping);
            e.Property(x => x.MaTopping).HasColumnName("ma_topping").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.MaMon).HasColumnName("ma_mon").HasMaxLength(10).IsRequired();
            e.Property(x => x.TenTopping).HasColumnName("ten_topping").HasMaxLength(100).IsUnicode().IsRequired();
            Money(e.Property(x => x.GiaBan), "gia_ban");
        });

        model.Entity<DinhLuong>(e =>
        {
            e.ToTable("DinhLuong");
            e.HasKey(x => x.MaDinhLuong);
            e.Property(x => x.MaDinhLuong).HasColumnName("ma_dinh_luong").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.MaMon).HasColumnName("ma_mon").HasMaxLength(10).IsRequired();
            e.Property(x => x.MaNguyenLieu).HasColumnName("ma_nguyen_lieu").HasMaxLength(10).IsRequired();
            e.Property(x => x.SoLuongSuDung).HasColumnName("so_luong_su_dung").HasPrecision(10, 3).IsRequired();
            e.HasIndex(x => new { x.MaMon, x.MaNguyenLieu }).IsUnique();
            e.HasOne(x => x.NguyenLieu).WithMany(x => x.DinhLuongs).HasForeignKey(x => x.MaNguyenLieu).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<NguyenLieu>(e =>
        {
            e.ToTable("NguyenLieu");
            e.HasKey(x => x.MaNguyenLieu);
            e.Property(x => x.MaNguyenLieu).HasColumnName("ma_nguyen_lieu").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.TenNguyenLieu).HasColumnName("ten_nguyen_lieu").HasMaxLength(100).IsUnicode().IsRequired();
            e.Property(x => x.DonViTinh).HasColumnName("don_vi_tinh").HasMaxLength(20).IsUnicode().IsRequired();
            e.Property(x => x.SoLuongTon).HasColumnName("so_luong_ton").HasPrecision(10, 3).IsRequired();
            e.Property(x => x.DinhMucToiThieu).HasColumnName("dinh_muc_toi_thieu").HasPrecision(10, 3).IsRequired();
            e.Property(x => x.HanSuDung).HasColumnName("han_su_dung").HasColumnType("date").IsRequired();
            e.Property<byte[]>("RowVersion").HasColumnName("row_version").IsRequired().IsRowVersion();
        });

        model.Entity<BienDongKho>(e =>
        {
            e.ToTable("BienDongKho", table =>
                table.HasCheckConstraint("CK_BienDongKho_loai_bien_dong", "[loai_bien_dong] IN ('Import', 'Export', 'Stocktake', 'Adjust')"));
            e.HasKey(x => x.MaBienDong);
            e.Property(x => x.MaBienDong).HasColumnName("ma_bien_dong").HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.MaNguyenLieu).HasColumnName("ma_nguyen_lieu").HasMaxLength(10).IsRequired();
            e.Property(x => x.MaNhanVien).HasColumnName("ma_nhan_vien").HasMaxLength(10).IsRequired();
            EnumProperty(e.Property(x => x.LoaiBienDong), "loai_bien_dong", 24);
            e.Property(x => x.SoLuong).HasColumnName("so_luong").HasPrecision(10, 3).IsRequired();
            e.Property(x => x.ThoiGian).HasColumnName("thoi_gian").HasColumnType("datetime").IsRequired();
            e.Property(x => x.LyDo).HasColumnName("ly_do").HasMaxLength(255).IsUnicode().IsRequired();
            e.HasOne(x => x.NguyenLieu).WithMany(x => x.BienDongKhos).HasForeignKey(x => x.MaNguyenLieu).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void EnumProperty<T>(PropertyBuilder<T> property, string columnName, int length) where T : struct, Enum =>
        property.HasColumnName(columnName).HasConversion<string>().HasMaxLength(length).IsUnicode(false).IsRequired();

    private static void Money(PropertyBuilder<decimal> property, string columnName) =>
        property.HasColumnName(columnName).HasPrecision(12, 2).IsRequired();
}

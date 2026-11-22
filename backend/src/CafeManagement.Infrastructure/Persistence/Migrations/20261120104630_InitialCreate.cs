using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Ban",
                schema: "dbo",
                columns: table => new
                {
                    ma_ban = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    khu_vuc = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    trang_thai = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    ghi_chu = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ban", x => x.ma_ban);
                    table.CheckConstraint("CK_Ban_trang_thai", "[trang_thai] IN ('Available', 'InUse', 'NeedsCleaning', 'Disabled')");
                });

            migrationBuilder.CreateTable(
                name: "DanhMuc",
                schema: "dbo",
                columns: table => new
                {
                    ma_danh_muc = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ten_danh_muc = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DanhMuc", x => x.ma_danh_muc);
                });

            migrationBuilder.CreateTable(
                name: "KhuyenMai",
                schema: "dbo",
                columns: table => new
                {
                    ma_khuyen_mai = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ten_chuong_trinh = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    loai_giam_gia = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    gia_tri_giam = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ngay_bat_dau = table.Column<DateOnly>(type: "date", nullable: false),
                    ngay_ket_thuc = table.Column<DateOnly>(type: "date", nullable: false),
                    gia_tri_don_toi_thieu = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    trang_thai = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhuyenMai", x => x.ma_khuyen_mai);
                    table.CheckConstraint("CK_KhuyenMai_loai_giam_gia", "[loai_giam_gia] IN ('Percentage', 'FixedAmount')");
                    table.CheckConstraint("CK_KhuyenMai_trang_thai", "[trang_thai] IN ('Active', 'Inactive', 'Expired')");
                });

            migrationBuilder.CreateTable(
                name: "NguyenLieu",
                schema: "dbo",
                columns: table => new
                {
                    ma_nguyen_lieu = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ten_nguyen_lieu = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    don_vi_tinh = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    so_luong_ton = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    dinh_muc_toi_thieu = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    han_su_dung = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NguyenLieu", x => x.ma_nguyen_lieu);
                });

            migrationBuilder.CreateTable(
                name: "NhanVien",
                schema: "dbo",
                columns: table => new
                {
                    ma_nhan_vien = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ho_ten = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    vi_tri = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    thong_tin_lien_he = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    trang_thai = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhanVien", x => x.ma_nhan_vien);
                    table.CheckConstraint("CK_NhanVien_trang_thai", "[trang_thai] IN ('Working', 'Resigned')");
                });

            migrationBuilder.CreateTable(
                name: "VaiTro",
                schema: "dbo",
                columns: table => new
                {
                    ma_vai_tro = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ten_vai_tro = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    danh_sach_quyen = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaiTro", x => x.ma_vai_tro);
                });

            migrationBuilder.CreateTable(
                name: "Mon",
                schema: "dbo",
                columns: table => new
                {
                    ma_mon = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_danh_muc = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ten_mon = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    mo_ta = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    hinh_anh = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    trang_thai = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mon", x => x.ma_mon);
                    table.CheckConstraint("CK_Mon_trang_thai", "[trang_thai] IN ('Available', 'OutOfStock', 'Discontinued')");
                    table.ForeignKey(
                        name: "FK_Mon_DanhMuc_ma_danh_muc",
                        column: x => x.ma_danh_muc,
                        principalSchema: "dbo",
                        principalTable: "DanhMuc",
                        principalColumn: "ma_danh_muc",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BienDongKho",
                schema: "dbo",
                columns: table => new
                {
                    ma_bien_dong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_nguyen_lieu = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_nhan_vien = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    loai_bien_dong = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    so_luong = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    thoi_gian = table.Column<DateTime>(type: "datetime", nullable: false),
                    ly_do = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BienDongKho", x => x.ma_bien_dong);
                    table.CheckConstraint("CK_BienDongKho_loai_bien_dong", "[loai_bien_dong] IN ('Import', 'Export', 'Stocktake', 'Adjust')");
                    table.ForeignKey(
                        name: "FK_BienDongKho_NguyenLieu_ma_nguyen_lieu",
                        column: x => x.ma_nguyen_lieu,
                        principalSchema: "dbo",
                        principalTable: "NguyenLieu",
                        principalColumn: "ma_nguyen_lieu",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BienDongKho_NhanVien_ma_nhan_vien",
                        column: x => x.ma_nhan_vien,
                        principalSchema: "dbo",
                        principalTable: "NhanVien",
                        principalColumn: "ma_nhan_vien",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DonHang",
                schema: "dbo",
                columns: table => new
                {
                    ma_don = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_ban = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    ma_nhan_vien = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    loai_don = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    thoi_gian_tao = table.Column<DateTime>(type: "datetime", nullable: false),
                    trang_thai = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    tam_tinh = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonHang", x => x.ma_don);
                    table.CheckConstraint("CK_DonHang_loai_don", "[loai_don] IN ('DineIn', 'TakeAway')");
                    table.CheckConstraint("CK_DonHang_trang_thai", "[trang_thai] IN ('Open', 'Confirmed', 'Preparing', 'Served', 'Paid', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_DonHang_Ban_ma_ban",
                        column: x => x.ma_ban,
                        principalSchema: "dbo",
                        principalTable: "Ban",
                        principalColumn: "ma_ban",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DonHang_NhanVien_ma_nhan_vien",
                        column: x => x.ma_nhan_vien,
                        principalSchema: "dbo",
                        principalTable: "NhanVien",
                        principalColumn: "ma_nhan_vien",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NhatKyThaoTac",
                schema: "dbo",
                columns: table => new
                {
                    ma_nhat_ky = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_nhan_vien = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    loai_thao_tac = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    doi_tuong_tac_dong = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    noi_dung = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    thoi_gian = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhatKyThaoTac", x => x.ma_nhat_ky);
                    table.ForeignKey(
                        name: "FK_NhatKyThaoTac_NhanVien_ma_nhan_vien",
                        column: x => x.ma_nhan_vien,
                        principalSchema: "dbo",
                        principalTable: "NhanVien",
                        principalColumn: "ma_nhan_vien",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaiKhoan",
                schema: "dbo",
                columns: table => new
                {
                    ten_dang_nhap = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ma_nhan_vien = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_vai_tro = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    mat_khau_hash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    trang_thai = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    thoi_gian_dang_nhap = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaiKhoan", x => x.ten_dang_nhap);
                    table.CheckConstraint("CK_TaiKhoan_trang_thai", "[trang_thai] IN ('Active', 'Locked', 'Disabled')");
                    table.ForeignKey(
                        name: "FK_TaiKhoan_NhanVien_ma_nhan_vien",
                        column: x => x.ma_nhan_vien,
                        principalSchema: "dbo",
                        principalTable: "NhanVien",
                        principalColumn: "ma_nhan_vien",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaiKhoan_VaiTro_ma_vai_tro",
                        column: x => x.ma_vai_tro,
                        principalSchema: "dbo",
                        principalTable: "VaiTro",
                        principalColumn: "ma_vai_tro",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DinhLuong",
                schema: "dbo",
                columns: table => new
                {
                    ma_dinh_luong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_mon = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_nguyen_lieu = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    so_luong_su_dung = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DinhLuong", x => x.ma_dinh_luong);
                    table.ForeignKey(
                        name: "FK_DinhLuong_Mon_ma_mon",
                        column: x => x.ma_mon,
                        principalSchema: "dbo",
                        principalTable: "Mon",
                        principalColumn: "ma_mon",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DinhLuong_NguyenLieu_ma_nguyen_lieu",
                        column: x => x.ma_nguyen_lieu,
                        principalSchema: "dbo",
                        principalTable: "NguyenLieu",
                        principalColumn: "ma_nguyen_lieu",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KichCo",
                schema: "dbo",
                columns: table => new
                {
                    ma_kich_co = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_mon = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ten_kich_co = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    gia_ban = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KichCo", x => x.ma_kich_co);
                    table.ForeignKey(
                        name: "FK_KichCo_Mon_ma_mon",
                        column: x => x.ma_mon,
                        principalSchema: "dbo",
                        principalTable: "Mon",
                        principalColumn: "ma_mon",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Topping",
                schema: "dbo",
                columns: table => new
                {
                    ma_topping = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_mon = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ten_topping = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    gia_ban = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topping", x => x.ma_topping);
                    table.ForeignKey(
                        name: "FK_Topping_Mon_ma_mon",
                        column: x => x.ma_mon,
                        principalSchema: "dbo",
                        principalTable: "Mon",
                        principalColumn: "ma_mon",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HoaDon",
                schema: "dbo",
                columns: table => new
                {
                    ma_hoa_don = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_don = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_nhan_vien = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_khuyen_mai = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    tong_tien_hang = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    thue_vat = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    phi_dich_vu = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    tien_giam_gia = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    tong_thanh_toan = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    phuong_thuc_thanh_toan = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    tien_khach_dua = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    tien_thua = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    thoi_gian_lap = table.Column<DateTime>(type: "datetime", nullable: false),
                    trang_thai = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoaDon", x => x.ma_hoa_don);
                    table.CheckConstraint("CK_HoaDon_phuong_thuc_thanh_toan", "[phuong_thuc_thanh_toan] IN ('Cash', 'BankTransfer', 'Card', 'EWallet')");
                    table.CheckConstraint("CK_HoaDon_trang_thai", "[trang_thai] IN ('Paid', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_HoaDon_DonHang_ma_don",
                        column: x => x.ma_don,
                        principalSchema: "dbo",
                        principalTable: "DonHang",
                        principalColumn: "ma_don",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoaDon_KhuyenMai_ma_khuyen_mai",
                        column: x => x.ma_khuyen_mai,
                        principalSchema: "dbo",
                        principalTable: "KhuyenMai",
                        principalColumn: "ma_khuyen_mai",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoaDon_NhanVien_ma_nhan_vien",
                        column: x => x.ma_nhan_vien,
                        principalSchema: "dbo",
                        principalTable: "NhanVien",
                        principalColumn: "ma_nhan_vien",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ThongBao",
                schema: "dbo",
                columns: table => new
                {
                    ma_thong_bao = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_don = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    ma_vai_tro = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    noi_dung = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    loai_thong_bao = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    thoi_gian = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThongBao", x => x.ma_thong_bao);
                    table.ForeignKey(
                        name: "FK_ThongBao_DonHang_ma_don",
                        column: x => x.ma_don,
                        principalSchema: "dbo",
                        principalTable: "DonHang",
                        principalColumn: "ma_don",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ThongBao_VaiTro_ma_vai_tro",
                        column: x => x.ma_vai_tro,
                        principalSchema: "dbo",
                        principalTable: "VaiTro",
                        principalColumn: "ma_vai_tro",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietDonHang",
                schema: "dbo",
                columns: table => new
                {
                    ma_chi_tiet = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_don = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_mon = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_kich_co = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    so_luong = table.Column<int>(type: "int", nullable: false),
                    don_gia = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ghi_chu = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    trang_thai_mon = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietDonHang", x => x.ma_chi_tiet);
                    table.CheckConstraint("CK_ChiTietDonHang_trang_thai_mon", "[trang_thai_mon] IN ('Waiting', 'Preparing', 'Completed', 'Served', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ChiTietDonHang_DonHang_ma_don",
                        column: x => x.ma_don,
                        principalSchema: "dbo",
                        principalTable: "DonHang",
                        principalColumn: "ma_don",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChiTietDonHang_KichCo_ma_kich_co",
                        column: x => x.ma_kich_co,
                        principalSchema: "dbo",
                        principalTable: "KichCo",
                        principalColumn: "ma_kich_co",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChiTietDonHang_Mon_ma_mon",
                        column: x => x.ma_mon,
                        principalSchema: "dbo",
                        principalTable: "Mon",
                        principalColumn: "ma_mon",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietTopping",
                schema: "dbo",
                columns: table => new
                {
                    ma_chi_tiet = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_topping = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietTopping", x => new { x.ma_chi_tiet, x.ma_topping });
                    table.ForeignKey(
                        name: "FK_ChiTietTopping_ChiTietDonHang_ma_chi_tiet",
                        column: x => x.ma_chi_tiet,
                        principalSchema: "dbo",
                        principalTable: "ChiTietDonHang",
                        principalColumn: "ma_chi_tiet",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChiTietTopping_Topping_ma_topping",
                        column: x => x.ma_topping,
                        principalSchema: "dbo",
                        principalTable: "Topping",
                        principalColumn: "ma_topping",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BienDongKho_ma_nguyen_lieu",
                schema: "dbo",
                table: "BienDongKho",
                column: "ma_nguyen_lieu");

            migrationBuilder.CreateIndex(
                name: "IX_BienDongKho_ma_nhan_vien",
                schema: "dbo",
                table: "BienDongKho",
                column: "ma_nhan_vien");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDonHang_ma_don",
                schema: "dbo",
                table: "ChiTietDonHang",
                column: "ma_don");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDonHang_ma_kich_co",
                schema: "dbo",
                table: "ChiTietDonHang",
                column: "ma_kich_co");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDonHang_ma_mon",
                schema: "dbo",
                table: "ChiTietDonHang",
                column: "ma_mon");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietTopping_ma_topping",
                schema: "dbo",
                table: "ChiTietTopping",
                column: "ma_topping");

            migrationBuilder.CreateIndex(
                name: "IX_DinhLuong_ma_mon_ma_nguyen_lieu",
                schema: "dbo",
                table: "DinhLuong",
                columns: new[] { "ma_mon", "ma_nguyen_lieu" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DinhLuong_ma_nguyen_lieu",
                schema: "dbo",
                table: "DinhLuong",
                column: "ma_nguyen_lieu");

            migrationBuilder.CreateIndex(
                name: "IX_DonHang_ma_ban",
                schema: "dbo",
                table: "DonHang",
                column: "ma_ban");

            migrationBuilder.CreateIndex(
                name: "IX_DonHang_ma_nhan_vien",
                schema: "dbo",
                table: "DonHang",
                column: "ma_nhan_vien");

            migrationBuilder.CreateIndex(
                name: "IX_HoaDon_ma_don",
                schema: "dbo",
                table: "HoaDon",
                column: "ma_don",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HoaDon_ma_khuyen_mai",
                schema: "dbo",
                table: "HoaDon",
                column: "ma_khuyen_mai");

            migrationBuilder.CreateIndex(
                name: "IX_HoaDon_ma_nhan_vien",
                schema: "dbo",
                table: "HoaDon",
                column: "ma_nhan_vien");

            migrationBuilder.CreateIndex(
                name: "IX_KichCo_ma_mon",
                schema: "dbo",
                table: "KichCo",
                column: "ma_mon");

            migrationBuilder.CreateIndex(
                name: "IX_Mon_ma_danh_muc",
                schema: "dbo",
                table: "Mon",
                column: "ma_danh_muc");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyThaoTac_ma_nhan_vien",
                schema: "dbo",
                table: "NhatKyThaoTac",
                column: "ma_nhan_vien");

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoan_ma_nhan_vien",
                schema: "dbo",
                table: "TaiKhoan",
                column: "ma_nhan_vien",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoan_ma_vai_tro",
                schema: "dbo",
                table: "TaiKhoan",
                column: "ma_vai_tro");

            migrationBuilder.CreateIndex(
                name: "IX_ThongBao_ma_don",
                schema: "dbo",
                table: "ThongBao",
                column: "ma_don");

            migrationBuilder.CreateIndex(
                name: "IX_ThongBao_ma_vai_tro",
                schema: "dbo",
                table: "ThongBao",
                column: "ma_vai_tro");

            migrationBuilder.CreateIndex(
                name: "IX_Topping_ma_mon",
                schema: "dbo",
                table: "Topping",
                column: "ma_mon");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BienDongKho",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ChiTietTopping",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DinhLuong",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "HoaDon",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "NhatKyThaoTac",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TaiKhoan",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ThongBao",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ChiTietDonHang",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Topping",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "NguyenLieu",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KhuyenMai",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "VaiTro",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DonHang",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KichCo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Ban",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "NhanVien",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Mon",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DanhMuc",
                schema: "dbo");
        }
    }
}

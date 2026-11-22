using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TableSessionsAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                schema: "dbo",
                table: "DonHang",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                schema: "dbo",
                table: "ChiTietDonHang",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                schema: "dbo",
                table: "Ban",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "DonHangBan",
                schema: "dbo",
                columns: table => new
                {
                    ma_don = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ma_ban = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    thoi_gian_bat_dau = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    thoi_gian_ket_thuc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonHangBan", x => new { x.ma_don, x.ma_ban, x.thoi_gian_bat_dau });
                    table.ForeignKey(
                        name: "FK_DonHangBan_Ban_ma_ban",
                        column: x => x.ma_ban,
                        principalSchema: "dbo",
                        principalTable: "Ban",
                        principalColumn: "ma_ban",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DonHangBan_DonHang_ma_don",
                        column: x => x.ma_don,
                        principalSchema: "dbo",
                        principalTable: "DonHang",
                        principalColumn: "ma_don",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO [dbo].[DonHangBan]
                    ([ma_don], [ma_ban], [thoi_gian_bat_dau], [thoi_gian_ket_thuc])
                SELECT [ma_don], [ma_ban], [thoi_gian_tao],
                    CASE WHEN [trang_thai] IN ('Paid', 'Cancelled') THEN [thoi_gian_tao] ELSE NULL END
                FROM [dbo].[DonHang]
                WHERE [ma_ban] IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DonHangBan_ma_ban",
                schema: "dbo",
                table: "DonHangBan",
                column: "ma_ban",
                unique: true,
                filter: "[thoi_gian_ket_thuc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DonHangBan_ma_don_thoi_gian_ket_thuc",
                schema: "dbo",
                table: "DonHangBan",
                columns: new[] { "ma_don", "thoi_gian_ket_thuc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonHangBan",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "dbo",
                table: "DonHang");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "dbo",
                table: "ChiTietDonHang");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "dbo",
                table: "Ban");
        }
    }
}

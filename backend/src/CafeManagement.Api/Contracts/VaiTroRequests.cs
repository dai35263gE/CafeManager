namespace CafeManagement.Api.Contracts;

public sealed record DanhSachQuyenResponse(string MaVaiTro, string TenVaiTro, IReadOnlyList<string> DanhSachQuyen);

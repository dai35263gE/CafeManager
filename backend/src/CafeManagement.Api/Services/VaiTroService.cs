using CafeManagement.Api.Contracts;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Services;

public sealed class VaiTroService(CafeDbContext db) : IVaiTroService
{
    public async Task<DanhSachQuyenResponse> LayDanhSachQuyenAsync(string id, CancellationToken ct)
    {
        var role = await db.VaiTros.AsNoTracking()
            .SingleOrDefaultAsync(x => x.MaVaiTro == id, ct)
            ?? throw ServiceHelpers.NotFound("Role");
        var permissions = role.DanhSachQuyen.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new(role.MaVaiTro, role.TenVaiTro, permissions);
    }
}

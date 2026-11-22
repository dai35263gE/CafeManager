using CafeManagement.Api.Contracts;
using CafeManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/vai-tro"), Authorize]
public sealed class VaiTroController(IVaiTroService service) : ApiController
{
    [HttpGet("{id}/quyen")]
    public async Task<ActionResult<ApiResponse<DanhSachQuyenResponse>>> LayDanhSachQuyen(
        string id, CancellationToken ct) =>
        Success(await service.LayDanhSachQuyenAsync(id, ct));
}

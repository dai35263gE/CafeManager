using CafeManagement.Api.Contracts;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/khuyen-mai"), Authorize(Roles = Roles.Manager + "," + Roles.Cashier)]
public sealed class KhuyenMaiController(IKhuyenMaiService service) : ApiController
{
    /// <summary>Search promotions by name/code and effective status.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<PromotionResponse>>>> TimKiem(
        [FromQuery] ListRequest request, PromotionStatus? status, CancellationToken ct) =>
        Success(await service.TimKiemKhuyenMaiAsync(request, status, ct));

    /// <summary>Get a promotion definition.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<PromotionResponse>>> Lay(string id, CancellationToken ct) =>
        Success(await service.LayKhuyenMaiAsync(id, ct));

    /// <summary>List invoices that used this promotion.</summary>
    [HttpGet("{id}/history"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<PageResponse<InvoiceResponse>>>> LayLichSuSuDung(
        string id, [FromQuery] ListRequest request, CancellationToken ct) =>
        Success(await service.LayLichSuSuDungAsync(id, request, ct));

    /// <summary>Summarize successful uses and discounts for a promotion.</summary>
    [HttpGet("{id}/history/summary"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<PromotionUsageSummaryResponse>>> LayThongKeSuDung(
        string id, CancellationToken ct) =>
        Success(await service.LayThongKeSuDungAsync(id, ct));

    /// <summary>Resume a paused promotion that is still within its effective period.</summary>
    [HttpPatch("{id}/resume"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<PromotionResponse>>> KichHoatLai(string id, CancellationToken ct) =>
        Success(await service.KichHoatLaiKhuyenMaiAsync(id, ct));

    /// <summary>End a promotion early without deleting its invoice relationships.</summary>
    [HttpPatch("{id}/end"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<PromotionResponse>>> KetThuc(string id, CancellationToken ct) =>
        Success(await service.KetThucKhuyenMaiAsync(id, ct));

    /// <summary>Create a promotion.</summary>
    [HttpPost, Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<PromotionResponse>>> Them(SavePromotionRequest request, CancellationToken ct)
    {
        var x = await service.ThemKhuyenMaiAsync(request, ct);
        return CreatedAtAction(nameof(Lay), new { id = x.Id }, new ApiResponse<PromotionResponse>(true, "Promotion created.", x));
    }

    /// <summary>Replace editable promotion fields; paid invoices retain monetary snapshots.</summary>
    [HttpPut("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<PromotionResponse>>> Sua(string id, SavePromotionRequest request, CancellationToken ct) =>
        Success(await service.SuaKhuyenMaiAsync(id, request, ct));

    /// <summary>Stop a promotion without deleting invoice relationships.</summary>
    [HttpDelete("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> TamNgung(string id, CancellationToken ct)
    {
        await service.TamNgungKhuyenMaiAsync(id, ct);
        return NoContent();
    }

    /// <summary>Preview a promotion against the server's actual order total.</summary>
    [HttpPost("validate")]
    public async Task<ActionResult<ApiResponse<PromotionValidationResponse>>> KiemTraHopLe(
        ValidatePromotionRequest request, CancellationToken ct) =>
        Success(await service.KiemTraHopLeAsync(request, ct));
}

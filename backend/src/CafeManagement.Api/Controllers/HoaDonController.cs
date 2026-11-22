using CafeManagement.Api.Contracts;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/hoa-don"), Authorize(Roles = Roles.Manager + "," + Roles.Cashier)]
public sealed class HoaDonController(IHoaDonService service) : ApiController
{
    /// <summary>Search paid or cancelled invoices in an optional [from,to) range.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<InvoiceResponse>>>> TimKiem(
        [FromQuery] ListRequest request, InvoiceStatus? status, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct) =>
        Success(await service.TimKiemHoaDonAsync(request, status, from, to, ct));

    /// <summary>Read invoice monetary snapshots and order line snapshots.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<InvoiceResponse>>> Lay(string id, CancellationToken ct) =>
        Success(await service.LayHoaDonAsync(id, ct));

    [HttpGet("quote")]
    public async Task<ActionResult<ApiResponse<InvoiceQuoteResponse>>> TaoBaoGia(
        [FromQuery] InvoiceQuoteRequest request, CancellationToken ct) =>
        Success(await service.TaoBaoGiaAsync(request, ct));

    /// <summary>Render the stored invoice as a printable plain-text receipt.</summary>
    [HttpGet("{id}/print")]
    public async Task<IActionResult> In(string id, CancellationToken ct) =>
        Content(await service.InHoaDonAsync(id, ct), "text/plain; charset=utf-8");

    /// <summary>Pay a served order once and release its table for cleaning, atomically.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<InvoiceResponse>>> Lap(CreateInvoiceRequest request, CancellationToken ct)
    {
        var x = await service.LapHoaDonAsync(request, ct);
        return CreatedAtAction(nameof(Lay), new { id = x.Id }, new ApiResponse<InvoiceResponse>(true, "Payment recorded.", x));
    }

    /// <summary>Cancel an invoice without editing monetary snapshots or reopening its order.</summary>
    [HttpPost("{id}/cancel"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<InvoiceResponse>>> Huy(string id, CancelInvoiceRequest request, CancellationToken ct) =>
        Success(await service.HuyHoaDonAsync(id, request, ct));
}

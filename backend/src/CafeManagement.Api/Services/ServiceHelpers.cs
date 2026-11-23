using System.Security.Claims;
using System.Security.Cryptography;
using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CafeManagement.Api.Services;

internal static class ServiceHelpers
{
    public static ApiException NotFound(string resource) => new(404, $"{resource} was not found.");
    public static ApiException Conflict(string message) => new(409, message);

    public static string Actor(IHttpContextAccessor accessor) =>
        accessor.HttpContext?.User.FindFirstValue("employee_id")
        ?? throw new ApiException(401, "Authentication required.");

    public static bool IsManager(IHttpContextAccessor accessor) =>
        accessor.HttpContext?.User.IsInRole(Roles.Manager) == true;

    public static bool CanManageOperations(IHttpContextAccessor accessor) =>
        IsManager(accessor) || accessor.HttpContext?.User.IsInRole(Roles.Admin) == true;

    public static void Audit(CafeDbContext db, string actorEmployeeId, string action, string entity, string id, string description)
    {
        const string alphabet = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        Span<char> suffix = stackalloc char[8];
        for (var index = 0; index < suffix.Length; index++)
            suffix[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        db.NhatKyThaoTacs.Add(new NhatKyThaoTac
        {
            MaNhatKy = $"NK{new string(suffix)}",
            MaNhanVien = actorEmployeeId,
            LoaiThaoTac = action,
            DoiTuongTacDong = entity,
            NoiDung = $"{id}: {description}",
            ThoiGian = DateTime.UtcNow
        });
    }

    public static void Version<T>(EntityEntry<T> entry, byte[] current, string expected) where T : class
    {
        byte[] token;
        try { token = Convert.FromBase64String(expected); }
        catch (FormatException) { throw new ApiException(400, "RowVersion must be a Base64 token."); }
        if (token.Length != 8)
            throw new ApiException(400, "RowVersion must contain eight bytes.");
        if (!current.SequenceEqual(token))
            throw Conflict("The record changed. Reload it before trying again.");
        entry.Property("RowVersion").OriginalValue = token;
    }

    public static void CloseTableLinks(DonHang order, TableStatus resultingStatus)
    {
        var now = DateTime.UtcNow;
        var links = order.DonHangBans.Where(link => link.ThoiGianKetThuc is null).ToList();
        foreach (var link in links)
        {
            link.ThoiGianKetThuc = now;
            link.Ban.TrangThai = resultingStatus;
        }
        if (links.Count == 0 && order.Ban is not null)
            order.Ban.TrangThai = resultingStatus;
    }

    public static void Money(decimal amount)
    {
        if (amount < 0 || amount > 999999999m || decimal.Round(amount, 2) != amount)
            throw new ApiException(400, "Price must be nonnegative, at most 999999999, with at most two decimal places.");
    }

    public static async Task<PageResponse<TResult>> PageAsync<T, TResult>(
        IQueryable<T> query, ListRequest request,
        System.Linq.Expressions.Expression<Func<T, TResult>> selector, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var items = offset > int.MaxValue ? [] : await query.Skip((int)offset)
            .Take(request.PageSize).Select(selector).ToListAsync(ct);
        return new PageResponse<TResult>(items, total, request.Page, request.PageSize);
    }
}

using System.Data;
using System.Security.Claims;
using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using CafeManagement.Infrastructure;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Services;

public sealed class DonHangService(
    CafeDbContext db,
    IdGenerator ids,
    IHttpContextAccessor accessor) : IDonHangService
{
    public async Task<PageResponse<OrderResponse>> TimKiemDonHangAsync(
        ListRequest request, OrderStatus? status, string? tableId, CancellationToken ct)
    {
        var query = OrderQuery().AsNoTracking();
        if (status.HasValue)
            query = query.Where(x => x.TrangThai == status);
        if (!string.IsNullOrWhiteSpace(tableId))
            query = query.Where(x => x.MaBan == tableId || x.DonHangBans.Any(link =>
                link.MaBan == tableId && link.ThoiGianKetThuc == null));
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.MaDon.Contains(request.Search.Trim()));
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.OrderByDescending(x => x.ThoiGianTao)
            .ThenBy(x => x.MaDon).Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        return new(await MapManyAsync(rows, ct), total, request.Page, request.PageSize);
    }

    public async Task<OrderResponse> LayDonHangAsync(string id, CancellationToken ct) =>
        await MapAsync(await FindAsync(id, ct), ct);

    public async Task<OrderResponse> TaoDonHangAsync(CreateOrderRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if ((request.Type == OrderType.DineIn && string.IsNullOrWhiteSpace(request.TableId)) ||
            (request.Type == OrderType.TakeAway && !string.IsNullOrWhiteSpace(request.TableId)))
            throw new ApiException(400, "Dine-in requires a table; takeaway must not specify a table.");
        if (request.Items.Count > 100)
            throw new ApiException(400, "An order supports at most 100 lines.");
        if (!string.IsNullOrWhiteSpace(request.Note))
            throw new ApiException(400, "Order notes are not part of the class diagram or ERD model.");
        if (request.Confirm && request.Items.Count == 0)
            throw new ApiException(400, "An order must contain at least one item before it can be confirmed.");

        var actor = ServiceHelpers.Actor(accessor);
        var order = new DonHang
        {
            MaDon = await ids.DonHangAsync(ct),
            LoaiDon = request.Type,
            ThoiGianTao = DateTime.UtcNow,
            TrangThai = request.Confirm ? OrderStatus.Confirmed : OrderStatus.Open,
            MaNhanVien = actor
        };
        if (!string.IsNullOrWhiteSpace(request.TableId))
        {
            var table = await FindTableAsync(request.TableId, ct);
            await RequireOrderableAsync(table, ct);
            table.TrangThai = TableStatus.InUse;
            order.MaBan = table.MaBan;
            order.Ban = table;
            order.DonHangBans.Add(new DonHangBan
            {
                MaDon = order.MaDon,
                DonHang = order,
                MaBan = table.MaBan,
                Ban = table,
                ThoiGianBatDau = order.ThoiGianTao
            });
        }
        foreach (var item in request.Items)
            order.ChiTiets.Add(await BuildLineAsync(order, item, ct));
        foreach (var line in order.ChiTiets)
            ServiceHelpers.Audit(db, actor, "OrderItemAdded", $"ChiTietDonHang:{line.MaChiTiet}",
                line.MaChiTiet, "Order item added to the order.");
        order.TamTinh = Subtotal(order);
        db.DonHangs.Add(order);
        if (request.Confirm)
            await AddOrderNoticesAsync(order, [Roles.Barista, Roles.Waiter, Roles.Manager],
                "Đơn mới đã được xác nhận và đang chờ pha chế.", ct);
        await SaveAsync(order, "OrderCreated",
            request.Confirm ? "Order created and confirmed." : "Draft order created.", ct);
        await transaction.CommitAsync(ct);
        return await MapAsync(order, ct);
    }

    public async Task<OrderResponse> ThemMonAsync(string id, AddOrderItemsRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await FindAsync(id, ct);
        RequireMutable(order);
        ServiceHelpers.Version(db.Entry(order), order.RowVersion, request.RowVersion);
        if (order.ChiTiets.Count + request.Items.Count > 100)
            throw new ApiException(400, "An order supports at most 100 lines.");
        var actor = ServiceHelpers.Actor(accessor);
        foreach (var item in request.Items)
        {
            var line = await BuildLineAsync(order, item, ct);
            order.ChiTiets.Add(line);
            ServiceHelpers.Audit(db, actor, "OrderItemAdded", $"ChiTietDonHang:{line.MaChiTiet}",
                line.MaChiTiet, "Order item added to the order.");
        }
        order.TamTinh = Subtotal(order);
        await AddOrderNoticesAsync(order, [Roles.Barista, Roles.Waiter, Roles.Manager],
            $"{request.Items.Count} món mới được thêm vào đơn.", ct);
        await SaveAsync(order, "ItemsAdded", $"{request.Items.Count} order lines added.", ct);
        await transaction.CommitAsync(ct);
        return await MapAsync(order, ct);
    }

    public async Task<OrderResponse> CapNhatChiTietMonAsync(
        string id, string itemId, UpdateOrderItemRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await FindAsync(id, ct);
        RequireMutable(order);
        var line = FindLine(order, itemId);
        ServiceHelpers.Version(db.Entry(line), line.RowVersion, request.RowVersion);
        if (line.TrangThaiMon != OrderItemStatus.Waiting)
            throw ServiceHelpers.Conflict("Only waiting items can change quantity.");
        line.SoLuong = request.Quantity;
        line.GhiChu = request.Note?.Trim() ?? string.Empty;
        order.TamTinh = Subtotal(order);
        await AddOrderNoticesAsync(order, [Roles.Barista, Roles.Waiter, Roles.Manager],
            $"Thông tin món {line.Mon.TenMon} trong đơn đã được cập nhật.", ct);
        await SaveAsync(order, "ItemUpdated", $"Line {itemId} quantity updated.", ct);
        await transaction.CommitAsync(ct);
        return await MapAsync(order, ct);
    }

    public Task<OrderResponse> HuyMonAsync(
        string id, string itemId, CancelItemRequest request, CancellationToken ct) =>
        CapNhatTrangThaiMonAsync(id, itemId, new ItemStatusRequest
        {
            Status = OrderItemStatus.Cancelled,
            RowVersion = request.RowVersion,
            Reason = request.Reason
        }, ct);

    public async Task<OrderResponse> CapNhatTrangThaiDonHangAsync(string id, OrderStatusRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await FindAsync(id, ct);
        RequireActive(order);
        ServiceHelpers.Version(db.Entry(order), order.RowVersion, request.RowVersion);
        switch (request.Status)
        {
            case OrderStatus.Confirmed:
                if (order.TrangThai != OrderStatus.Open ||
                    !order.ChiTiets.Any(x => x.TrangThaiMon != OrderItemStatus.Cancelled))
                    throw ServiceHelpers.Conflict("Only a nonempty draft order can be confirmed.");
                order.TrangThai = OrderStatus.Confirmed;
                await AddOrderNoticesAsync(order, [Roles.Barista, Roles.Waiter, Roles.Manager],
                    "Đơn mới đã được xác nhận và đang chờ pha chế.", ct);
                break;
            case OrderStatus.Served:
                if (order.TrangThai is not (OrderStatus.Confirmed or OrderStatus.Preparing) ||
                    !order.ChiTiets.Any(x => x.TrangThaiMon != OrderItemStatus.Cancelled) ||
                    order.ChiTiets.Any(x => x.TrangThaiMon is not (OrderItemStatus.Served or OrderItemStatus.Cancelled)))
                    throw ServiceHelpers.Conflict("All active items must be served first.");
                order.TrangThai = OrderStatus.Served;
                await AddOrderNoticesAsync(order, [Roles.Cashier, Roles.Waiter, Roles.Manager],
                    "Tất cả món đã phục vụ; đơn sẵn sàng thanh toán.", ct);
                break;
            case OrderStatus.Cancelled:
                RequireReason(request.Reason);
                if (!ServiceHelpers.CanManageOperations(accessor) &&
                    order.ChiTiets.Any(x => x.TrangThaiMon is OrderItemStatus.Preparing or OrderItemStatus.Completed or OrderItemStatus.Served))
                    throw new ApiException(403, "Only Manager can cancel an order after preparation starts.");
                foreach (var line in order.ChiTiets)
                    line.TrangThaiMon = OrderItemStatus.Cancelled;
                order.TrangThai = OrderStatus.Cancelled;
                order.TamTinh = 0;
                ServiceHelpers.CloseTableLinks(order, TableStatus.NeedsCleaning);
                await AddOrderNoticesAsync(order, [Roles.Barista, Roles.Waiter, Roles.Cashier, Roles.Manager],
                    "Đơn đã bị hủy.", ct);
                break;
            default:
                throw new ApiException(400, "Use Confirmed, Served or Cancelled. Payment has a dedicated operation.");
        }
        await SaveAsync(order, "OrderStatusChanged", $"Order status changed to {request.Status}.", ct);
        await transaction.CommitAsync(ct);
        return await MapAsync(order, ct);
    }

    public async Task<OrderResponse> CapNhatTrangThaiMonAsync(
        string id, string itemId, ItemStatusRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await FindAsync(id, ct);
        RequireMutable(order);
        var line = FindLine(order, itemId);
        ServiceHelpers.Version(db.Entry(line), line.RowVersion, request.RowVersion);
        var manager = ServiceHelpers.CanManageOperations(accessor);
        var user = accessor.HttpContext?.User ?? throw new ApiException(401, "Authentication required.");
        switch (request.Status)
        {
            case OrderItemStatus.Preparing:
                if (!manager && !user.IsInRole(Roles.Barista))
                    throw new ApiException(403, "Kitchen role required.");
                if (order.TrangThai == OrderStatus.Open || line.TrangThaiMon != OrderItemStatus.Waiting)
                    throw ServiceHelpers.Conflict("Only confirmed waiting items can start preparation.");
                break;
            case OrderItemStatus.Completed:
                if (!manager && !user.IsInRole(Roles.Barista))
                    throw new ApiException(403, "Kitchen role required.");
                if (line.TrangThaiMon != OrderItemStatus.Preparing)
                    throw ServiceHelpers.Conflict("Only preparing items can be completed.");
                if (!manager)
                {
                    var owner = await db.NhatKyThaoTacs.AsNoTracking()
                        .Where(x => x.LoaiThaoTac == "ItemClaimed" &&
                            x.DoiTuongTacDong == $"ChiTietDonHang:{line.MaChiTiet}")
                        .OrderByDescending(x => x.ThoiGian)
                        .Select(x => x.MaNhanVien)
                        .FirstOrDefaultAsync(ct);
                    if (owner is not null && owner != ServiceHelpers.Actor(accessor))
                        throw new ApiException(403, "Only the barista who claimed this item can complete it.");
                }
                break;
            case OrderItemStatus.Served:
                if (!manager && !user.IsInRole(Roles.Waiter))
                    throw new ApiException(403, "Floor role required.");
                if (line.TrangThaiMon != OrderItemStatus.Completed)
                    throw ServiceHelpers.Conflict("Only completed items can be served.");
                break;
            case OrderItemStatus.Cancelled:
                if (!manager && !user.IsInRole(Roles.Waiter))
                    throw new ApiException(403, "Floor role required.");
                RequireReason(request.Reason);
                if (!manager && line.TrangThaiMon != OrderItemStatus.Waiting)
                    throw new ApiException(403, "Only Manager can cancel an item after preparation starts.");
                if (order.TrangThai != OrderStatus.Open &&
                    order.ChiTiets.Count(x => x.TrangThaiMon != OrderItemStatus.Cancelled) == 1)
                    throw ServiceHelpers.Conflict("Cancel the whole order instead of its last active line.");
                break;
            default:
                throw new ApiException(400, "Invalid item status transition.");
        }

        line.TrangThaiMon = request.Status;
        order.TamTinh = Subtotal(order);
        RefreshOrderStatus(order);
        var actor = ServiceHelpers.Actor(accessor);
        if (request.Status == OrderItemStatus.Preparing)
            ServiceHelpers.Audit(db, actor, "ItemClaimed", $"ChiTietDonHang:{line.MaChiTiet}",
                line.MaChiTiet, "Item claimed for preparation.");
        var recipients = request.Status switch
        {
            OrderItemStatus.Preparing => new[] { Roles.Barista, Roles.Waiter, Roles.Manager },
            OrderItemStatus.Completed => new[] { Roles.Cashier, Roles.Waiter, Roles.Manager },
            OrderItemStatus.Served => new[] { Roles.Cashier, Roles.Waiter, Roles.Manager },
            _ => new[] { Roles.Barista, Roles.Waiter, Roles.Cashier, Roles.Manager }
        };
        await AddOrderNoticesAsync(order, recipients,
            $"Món {line.Mon.TenMon} chuyển sang trạng thái {request.Status}.", ct);
        await SaveAsync(order, "ItemStatusChanged", $"Line {itemId} changed to {request.Status}.", ct);
        await transaction.CommitAsync(ct);
        return await MapAsync(order, ct);
    }

    public async Task<OrderResponse> MoLaiMonAsync(
        string id, string itemId, ReopenOrderItemRequest request, CancellationToken ct)
    {
        if (!ServiceHelpers.CanManageOperations(accessor))
            throw new ApiException(403, "Manager role required.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await FindAsync(id, ct);
        RequireMutable(order);
        var line = FindLine(order, itemId);
        ServiceHelpers.Version(db.Entry(line), line.RowVersion, request.RowVersion);
        if (line.TrangThaiMon != OrderItemStatus.Cancelled)
            throw ServiceHelpers.Conflict("Only cancelled items can be reopened.");
        RequireReason(request.Reason);

        line.TrangThaiMon = OrderItemStatus.Waiting;
        order.TamTinh = Subtotal(order);
        RefreshOrderStatus(order);
        var message = $"Quản lý mở lại món {line.Mon.TenMon}. Lý do: {request.Reason.Trim()}";
        await AddOrderNoticesAsync(order, [Roles.Barista, Roles.Waiter, Roles.Manager], message, ct);
        await SaveAsync(order, "ItemReopened", $"Line {itemId} reopened. Reason: {request.Reason.Trim()}", ct);
        await transaction.CommitAsync(ct);
        return await MapAsync(order, ct);
    }

    public async Task<OrderResponse> ChuyenBanAsync(
        string id, TransferOrderRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await FindAsync(id, ct);
        if (string.IsNullOrWhiteSpace(order.MaBan))
            throw ServiceHelpers.Conflict("This order is not linked to a table.");
        var result = await TransferAsync(order, order.MaBan, request.TargetTableId, request.RowVersion,
            request.SourceTableRowVersion, request.TargetTableRowVersion, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<OrderResponse> ChuyenBanTheoBanAsync(TransferTableRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await ActiveTableOrderAsync(request.SourceTableId, ct);
        if (order is null)
            throw ServiceHelpers.NotFound("Active table order");
        var result = await TransferAsync(order, request.SourceTableId, request.TargetTableId,
            request.OrderRowVersion, request.SourceTableRowVersion, request.TargetTableRowVersion, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<OrderResponse> GhepDonAsync(string id, MergeOrderRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (id == request.TargetOrderId)
            throw new ApiException(400, "Cannot merge an order into itself.");
        var source = await FindAsync(id, ct);
        var target = await FindAsync(request.TargetOrderId, ct);
        RequireMergeable(source);
        RequireMergeable(target);
        ServiceHelpers.Version(db.Entry(source), source.RowVersion, request.RowVersion);
        ServiceHelpers.Version(db.Entry(target), target.RowVersion, request.TargetOrderRowVersion);
        ValidateMergeOrders(source, target);
        MoveOrderLines(source, target);
        source.TrangThai = OrderStatus.Cancelled;
        source.TamTinh = 0;
        ServiceHelpers.CloseTableLinks(source, TableStatus.NeedsCleaning);
        target.TamTinh = Subtotal(target);
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "OrderMerged", "DonHang",
            source.MaDon, $"Merged into {target.MaDon}.");
        await AddOrderNoticesAsync(target, [Roles.Barista, Roles.Waiter, Roles.Manager],
            $"Đơn {source.MaDon} đã được ghép vào đơn này.", ct);
        await SaveAsync(target, "OrderMergeReceived", $"Received lines from {source.MaDon}.", ct);
        await transaction.CommitAsync(ct);
        return await MapAsync(target, ct);
    }

    public async Task<MergeTablesResponse> GhepDonTheoBanAsync(MergeTableRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var targetTable = await FindTableAsync(request.TargetTableId, ct);
        ServiceHelpers.Version(db.Entry(targetTable), targetTable.RowVersion, request.TargetTableRowVersion);
        ValidateMergeableTable(targetTable);

        var sourceIds = request.Sources.Select(x => x.TableId).ToList();
        if (sourceIds.Count is < 1 or > 19 || sourceIds.Contains(targetTable.MaBan) ||
            sourceIds.Distinct(StringComparer.Ordinal).Count() != sourceIds.Count)
            throw new ApiException(400, "Choose between one and nineteen different source tables, excluding the main table.");

        var tables = new List<Ban> { targetTable };
        foreach (var sourceRequest in request.Sources)
        {
            var sourceTable = await FindTableAsync(sourceRequest.TableId, ct);
            ServiceHelpers.Version(db.Entry(sourceTable), sourceTable.RowVersion, sourceRequest.TableRowVersion);
            ValidateMergeableTable(sourceTable);
            tables.Add(sourceTable);
        }

        var activeOrdersByTable = new Dictionary<string, DonHang?>(StringComparer.Ordinal)
        {
            [targetTable.MaBan] = await ActiveTableOrderAsync(targetTable.MaBan, ct)
        };
        ValidateOrderToken(activeOrdersByTable[targetTable.MaBan], request.TargetActiveOrderRowVersion);
        foreach (var sourceRequest in request.Sources)
        {
            var order = await ActiveTableOrderAsync(sourceRequest.TableId, ct);
            ValidateOrderToken(order, sourceRequest.ActiveOrderRowVersion);
            activeOrdersByTable.Add(sourceRequest.TableId, order);
        }

        foreach (var table in tables)
        {
            if (activeOrdersByTable[table.MaBan] is not null && table.TrangThai != TableStatus.InUse)
                throw ServiceHelpers.Conflict("A table with an active order must be marked InUse.");
        }
        var orders = activeOrdersByTable.Values.Where(order => order is not null)
            .Select(order => order!).DistinctBy(order => order.MaDon).ToList();
        DonHang? destinationOrder;
        if (!string.IsNullOrWhiteSpace(request.TargetOrderId))
        {
            destinationOrder = orders.SingleOrDefault(order => order.MaDon == request.TargetOrderId)
                ?? throw ServiceHelpers.Conflict("The selected destination order is not active on these tables.");
            if (string.IsNullOrWhiteSpace(request.TargetOrderRowVersion))
                throw new ApiException(400, "The destination order RowVersion is required.");
            ServiceHelpers.Version(db.Entry(destinationOrder), destinationOrder.RowVersion, request.TargetOrderRowVersion);
        }
        else
        {
            if (orders.Count > 1)
                throw new ApiException(400, "Select a destination order before merging multiple active orders.");
            destinationOrder = activeOrdersByTable[targetTable.MaBan] ??
                (orders.Count == 1 ? orders[0] : null);
        }

        if (destinationOrder is not null)
        {
            foreach (var order in orders)
                RequireMergeable(order);
            if (orders.Any(order => order!.LoaiDon != OrderType.DineIn))
                throw new ApiException(400, "Only dine-in orders can be merged by table.");
            if (orders.Any(order => (order!.TrangThai == OrderStatus.Open) != (destinationOrder.TrangThai == OrderStatus.Open)))
                throw ServiceHelpers.Conflict("Confirm all orders or leave all as drafts before merging.");
            if (orders.Sum(order => order!.ChiTiets.Count) > 100)
                throw new ApiException(400, "The merged order would exceed 100 lines.");

            foreach (var sourceOrder in orders.Where(order => order!.MaDon != destinationOrder.MaDon))
            {
                MoveOrderLines(sourceOrder!, destinationOrder);
                MoveActiveTableLinks(sourceOrder!, destinationOrder);
                sourceOrder!.TrangThai = OrderStatus.Cancelled;
                sourceOrder.TamTinh = 0;
                ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "OrderMerged", "DonHang",
                    sourceOrder.MaDon, $"Merged into {destinationOrder.MaDon} during table merge.");
            }
            foreach (var table in tables)
                AddActiveTableLink(destinationOrder, table);
            destinationOrder.MaBan = targetTable.MaBan;
            destinationOrder.Ban = targetTable;
            destinationOrder.TamTinh = Subtotal(destinationOrder);
            await AddOrderNoticesAsync(destinationOrder, [Roles.Barista, Roles.Waiter, Roles.Manager],
                $"Bàn được ghép; các bàn nguồn: {string.Join(", ", sourceIds)}.", ct);
        }

        foreach (var table in tables)
            table.TrangThai = TableStatus.InUse;
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "TablesMerged", "Ban",
            targetTable.MaBan, $"Merged with {string.Join(", ", sourceIds)}.");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(targetTable.MaBan, sourceIds,
            destinationOrder is null ? null : await MapAsync(destinationOrder, ct));
    }

    public async Task<PageResponse<KitchenItemResponse>> XemHangDoiBepAsync(
        ListRequest request, OrderItemStatus? status, CancellationToken ct)
    {
        var cancellationCutoff = DateTime.UtcNow.AddHours(-24);
        var cancellationAudits = await db.NhatKyThaoTacs.AsNoTracking()
            .Where(x => x.ThoiGian >= cancellationCutoff &&
                ((x.LoaiThaoTac == "OrderStatusChanged" && x.NoiDung.Contains("Order status changed to Cancelled.")) ||
                 (x.LoaiThaoTac == "ItemStatusChanged" && x.NoiDung.Contains("changed to Cancelled."))))
            .Select(x => new { x.LoaiThaoTac, x.NoiDung })
            .ToListAsync(ct);
        var cancelledOrderIds = cancellationAudits
            .Where(x => x.LoaiThaoTac == "OrderStatusChanged")
            .Select(x => x.NoiDung.Split(':', 2)[0])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var cancelledItemIds = cancellationAudits
            .Where(x => x.LoaiThaoTac == "ItemStatusChanged")
            .Select(x => CancelledItemId(x.NoiDung))
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var query = db.ChiTietDonHangs.AsNoTracking().Include(x => x.Mon).Include(x => x.KichCo)
            .Include(x => x.DonHang).ThenInclude(x => x.Ban)
            .Include(x => x.ChiTietToppings).ThenInclude(x => x.Topping)
            .Where(x => x.DonHang.TrangThai != OrderStatus.Open &&
                x.DonHang.TrangThai != OrderStatus.Paid &&
                x.TrangThaiMon != OrderItemStatus.Served &&
                (x.TrangThaiMon != OrderItemStatus.Cancelled ||
                 cancelledOrderIds.Contains(x.MaDon) || cancelledItemIds.Contains(x.MaChiTiet)));
        if (status.HasValue)
            query = query.Where(x => x.TrangThaiMon == status);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.MaMon.Contains(request.Search.Trim()) ||
                x.DonHang.MaDon.Contains(request.Search.Trim()));
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var candidates = await query.Select(x => new
        {
            x.MaChiTiet,
            x.DonHang.ThoiGianTao
        }).ToListAsync(ct);
        var callTargets = candidates.Select(x => $"ChiTietDonHang:{x.MaChiTiet}").ToArray();
        var callTimes = await db.NhatKyThaoTacs.AsNoTracking()
            .Where(x => x.LoaiThaoTac == "OrderItemAdded" && callTargets.Contains(x.DoiTuongTacDong))
            .Select(x => new { x.DoiTuongTacDong, x.ThoiGian })
            .ToListAsync(ct);
        var callTimeByTarget = callTimes.GroupBy(x => x.DoiTuongTacDong, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Min(x => x.ThoiGian), StringComparer.Ordinal);
        List<string> pageIds = offset > int.MaxValue ? [] : candidates
            .OrderBy(x => callTimeByTarget.GetValueOrDefault(
                $"ChiTietDonHang:{x.MaChiTiet}", x.ThoiGianTao))
            .ThenBy(x => x.MaChiTiet, StringComparer.Ordinal)
            .Skip((int)offset).Take(request.PageSize)
            .Select(x => x.MaChiTiet).ToList();
        List<ChiTietDonHang> rows = pageIds.Count == 0
            ? []
            : await query.Where(x => pageIds.Contains(x.MaChiTiet)).ToListAsync(ct);
        var rowById = rows.ToDictionary(x => x.MaChiTiet, StringComparer.Ordinal);
        rows = pageIds.Where(rowById.ContainsKey).Select(id => rowById[id]).ToList();
        var owners = await PreparedOwnersAsync(rows, ct);
        var items = rows.Select(x =>
        {
            var calledAt = callTimeByTarget.GetValueOrDefault(
                $"ChiTietDonHang:{x.MaChiTiet}", x.DonHang.ThoiGianTao);
            return new KitchenItemResponse(
                x.MaDon, x.DonHang.MaDon, x.DonHang.MaBan, x.DonHang.Ban?.MaBan, null,
                x.DonHang.TrangThai,
                new DateTimeOffset(DateTime.SpecifyKind(calledAt, DateTimeKind.Utc)),
                MapLineWithOwner(x, owners.GetValueOrDefault(x.MaChiTiet)));
        }).ToList();
        return new(items, total, request.Page, request.PageSize);
    }

    public async Task<KitchenNoticeResponse> BaoThieuAsync(ShortageReportRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (string.IsNullOrWhiteSpace(request.MenuItemId) && string.IsNullOrWhiteSpace(request.IngredientId))
            throw new ApiException(400, "Select a menu item or ingredient.");
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Chưa cung cấp lý do." : request.Reason.Trim();
        var actor = ServiceHelpers.Actor(accessor);
        var reporter = await db.TaiKhoans.AsNoTracking()
            .Where(x => x.MaNhanVien == actor)
            .Select(x => x.TenDangNhap)
            .SingleOrDefaultAsync(ct) ?? actor;
        var reportTime = DateTime.UtcNow;
        var description = reason;
        string entityId;
        if (!string.IsNullOrWhiteSpace(request.MenuItemId))
        {
            var menu = await db.Mons.SingleOrDefaultAsync(x => x.MaMon == request.MenuItemId, ct)
                ?? throw ServiceHelpers.NotFound("Menu item");
            if (menu.TrangThai == MenuItemStatus.Discontinued)
                throw ServiceHelpers.Conflict("The menu item is discontinued.");
            if (menu.TrangThai == MenuItemStatus.OutOfStock)
                throw ServiceHelpers.Conflict("The menu item has already been reported out of stock.");
            var affectedOrders = await db.ChiTietDonHangs.AsNoTracking()
                .Where(line => line.MaMon == menu.MaMon &&
                    (line.TrangThaiMon == OrderItemStatus.Waiting || line.TrangThaiMon == OrderItemStatus.Preparing) &&
                    (line.DonHang.TrangThai == OrderStatus.Confirmed ||
                     line.DonHang.TrangThai == OrderStatus.Preparing))
                .Select(line => new { line.DonHang.MaDon, line.DonHang.MaBan })
                .Distinct()
                .OrderBy(order => order.MaDon)
                .ToListAsync(ct);
            menu.TrangThai = MenuItemStatus.OutOfStock;
            entityId = menu.MaMon;
            var affected = affectedOrders.Count == 0
                ? "Không có đơn đang chờ bị ảnh hưởng."
                : $"Đơn đang bị ảnh hưởng: {string.Join(", ", affectedOrders.Select(x => $"{x.MaDon} ({x.MaBan ?? "Mang đi"})"))}.";
            description = $"{menu.TenMon}: {reason}. Người báo: {reporter}; thời điểm: {reportTime:O}. {affected}";
        }
        else
        {
            var ingredient = await db.NguyenLieus.SingleOrDefaultAsync(x => x.MaNguyenLieu == request.IngredientId, ct)
                ?? throw ServiceHelpers.NotFound("Ingredient");
            entityId = ingredient.MaNguyenLieu;
            description = $"{ingredient.TenNguyenLieu}: {reason}. Người báo: {reporter}; thời điểm: {reportTime:O}.";
        }
        var notice = await AddNoticeAsync(Roles.Barista, null, $"{entityId}: {description}", "Shortage", ct);
        await AddNoticesAsync([Roles.Waiter, Roles.Manager], null,
            $"{entityId}: {description}", "Shortage", ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(notice.MaThongBao, null, entityId, description, false,
            new DateTimeOffset(DateTime.SpecifyKind(notice.ThoiGian, DateTimeKind.Utc)));
    }

    public async Task<PageResponse<KitchenNoticeResponse>> TimKiemThongBaoAsync(
        ListRequest request, DateTimeOffset? since, CancellationToken ct)
    {
        var roleName = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role)
            ?? throw new ApiException(401, "Authentication required.");
        var roleId = await db.VaiTros.AsNoTracking()
            .Where(x => x.TenVaiTro == roleName)
            .Select(x => x.MaVaiTro)
            .SingleOrDefaultAsync(ct)
            ?? throw new ApiException(403, "The account role is not configured.");
        var query = db.ThongBaos.AsNoTracking().Where(x => roleName == Roles.Admin || x.MaVaiTro == roleId);
        if (since.HasValue)
        {
            var start = since.Value.UtcDateTime;
            query = query.Where(x => x.ThoiGian >= start);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.NoiDung.Contains(request.Search.Trim()));
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.OrderByDescending(x => x.ThoiGian)
            .Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        var targets = rows.Select(row => $"ThongBao:{row.MaThongBao}").ToArray();
        var actor = ServiceHelpers.Actor(accessor);
        var readTargets = await db.NhatKyThaoTacs.AsNoTracking()
            .Where(x => x.LoaiThaoTac == "NoticeRead" && x.MaNhanVien == actor &&
                targets.Contains(x.DoiTuongTacDong))
            .Select(x => x.DoiTuongTacDong)
            .ToListAsync(ct);
        var readSet = readTargets.ToHashSet(StringComparer.Ordinal);
        var items = rows.Select(row => MapNotice(row,
            readSet.Contains($"ThongBao:{row.MaThongBao}"))).ToList();
        return new(items, total, request.Page, request.PageSize);
    }

    public async Task<KitchenNoticeResponse> DanhDauThongBaoDaDocAsync(string id, CancellationToken ct)
    {
        var roleName = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role)
            ?? throw new ApiException(401, "Authentication required.");
        var actor = ServiceHelpers.Actor(accessor);
        var roleId = await db.VaiTros.AsNoTracking()
            .Where(x => x.TenVaiTro == roleName)
            .Select(x => x.MaVaiTro)
            .SingleOrDefaultAsync(ct)
            ?? throw new ApiException(403, "The account role is not configured.");
        var notice = await db.ThongBaos.SingleOrDefaultAsync(x =>
            x.MaThongBao == id && (roleName == Roles.Admin || x.MaVaiTro == roleId), ct)
            ?? throw ServiceHelpers.NotFound("Notification");
        var target = $"ThongBao:{notice.MaThongBao}";
        var alreadyRead = await db.NhatKyThaoTacs.AnyAsync(x =>
            x.LoaiThaoTac == "NoticeRead" && x.MaNhanVien == actor &&
            x.DoiTuongTacDong == target, ct);
        if (!alreadyRead)
        {
            ServiceHelpers.Audit(db, actor, "NoticeRead", target, notice.MaThongBao,
                "Notification acknowledged as read.");
            await db.SaveChangesAsync(ct);
        }
        var separator = notice.NoiDung.IndexOf(": ", StringComparison.Ordinal);
        var entityId = notice.MaDon ??
            (separator >= 0 ? notice.NoiDung[..separator] : null);
        var description = separator >= 0 ? notice.NoiDung[(separator + 2)..] : notice.NoiDung;
        return new(notice.MaThongBao, null, entityId, description, true,
            new DateTimeOffset(DateTime.SpecifyKind(notice.ThoiGian, DateTimeKind.Utc)));
    }

    private IQueryable<DonHang> OrderQuery() => db.DonHangs.Include(x => x.Ban)
        .Include(x => x.DonHangBans).ThenInclude(x => x.Ban)
        .Include(x => x.ChiTiets).ThenInclude(x => x.Mon)
        .Include(x => x.ChiTiets).ThenInclude(x => x.KichCo)
        .Include(x => x.ChiTiets).ThenInclude(x => x.ChiTietToppings).ThenInclude(x => x.Topping)
        .AsSplitQuery();

    private async Task<DonHang> FindAsync(string id, CancellationToken ct) =>
        await OrderQuery().SingleOrDefaultAsync(x => x.MaDon == id, ct) ?? throw ServiceHelpers.NotFound("Order");

    private async Task<Ban> FindTableAsync(string id, CancellationToken ct) =>
        await db.Bans.SingleOrDefaultAsync(x => x.MaBan == id, ct) ?? throw ServiceHelpers.NotFound("Table");

    private async Task RequireAvailableAsync(Ban table, CancellationToken ct)
    {
        if (table.TrangThai != TableStatus.Available ||
            await HasActiveOrderAsync(table.MaBan, ct))
            throw ServiceHelpers.Conflict("The destination table is not available.");
    }

    private async Task RequireOrderableAsync(Ban table, CancellationToken ct)
    {
        if (table.TrangThai is not (TableStatus.Available or TableStatus.InUse) ||
            await HasActiveOrderAsync(table.MaBan, ct))
            throw ServiceHelpers.Conflict("The table is not available for a new order.");
    }

    private Task<bool> HasActiveOrderAsync(string tableId, CancellationToken ct) =>
        db.DonHangBans.AnyAsync(link => link.MaBan == tableId && link.ThoiGianKetThuc == null &&
            link.DonHang.TrangThai != OrderStatus.Paid && link.DonHang.TrangThai != OrderStatus.Cancelled, ct);

    private Task<DonHang?> ActiveTableOrderAsync(string tableId, CancellationToken ct) =>
        OrderQuery().SingleOrDefaultAsync(order => order.DonHangBans.Any(link =>
            link.MaBan == tableId && link.ThoiGianKetThuc == null &&
            order.TrangThai != OrderStatus.Paid && order.TrangThai != OrderStatus.Cancelled), ct);

    private async Task<OrderResponse> TransferAsync(
        DonHang order, string sourceTableId, string targetTableId, string orderToken,
        string sourceTableToken, string targetTableToken, CancellationToken ct)
    {
        RequireMergeable(order);
        ServiceHelpers.Version(db.Entry(order), order.RowVersion, orderToken);
        if (order.LoaiDon != OrderType.DineIn || sourceTableId == targetTableId)
            throw new ApiException(400, "Transfer requires two different tables and a dine-in order.");

        var source = await FindTableAsync(sourceTableId, ct);
        var target = await FindTableAsync(targetTableId, ct);
        ServiceHelpers.Version(db.Entry(source), source.RowVersion, sourceTableToken);
        ServiceHelpers.Version(db.Entry(target), target.RowVersion, targetTableToken);
        if (source.TrangThai != TableStatus.InUse)
            throw ServiceHelpers.Conflict("The source table is no longer occupied.");
        var sourceLink = order.DonHangBans.SingleOrDefault(link =>
            link.MaBan == source.MaBan && link.ThoiGianKetThuc is null)
            ?? throw ServiceHelpers.Conflict("The source table is no longer linked to this order.");
        await RequireAvailableAsync(target, ct);

        var now = DateTime.UtcNow;
        sourceLink.ThoiGianKetThuc = now;
        source.TrangThai = TableStatus.NeedsCleaning;
        target.TrangThai = TableStatus.InUse;
        order.DonHangBans.Add(new DonHangBan
        {
            MaDon = order.MaDon,
            DonHang = order,
            MaBan = target.MaBan,
            Ban = target,
            ThoiGianBatDau = now
        });
        if (order.MaBan == source.MaBan)
        {
            order.Ban = target;
            order.MaBan = target.MaBan;
        }
        await AddOrderNoticesAsync(order, [Roles.Barista, Roles.Waiter, Roles.Manager],
            $"Đơn đã được chuyển từ bàn {source.MaBan} sang bàn {target.MaBan}.", ct);
        await SaveAsync(order, "OrderTransferred", $"Moved from {source.MaBan} to {target.MaBan}.", ct);
        return await MapAsync(order, ct);
    }

    private async Task<ChiTietDonHang> BuildLineAsync(
        DonHang order, CreateOrderItemRequest request, CancellationToken ct)
    {
        var menu = await db.Mons.Include(x => x.KichCos).Include(x => x.Toppings)
            .SingleOrDefaultAsync(x => x.MaMon == request.MenuItemId, ct)
            ?? throw new ApiException(400, "The menu item does not exist.");
        if (menu.TrangThai != MenuItemStatus.Available)
            throw ServiceHelpers.Conflict("The menu item is not available for ordering.");

        if (menu.KichCos.Count == 0)
            throw new ApiException(400, "The menu item has no valid price.");
        var size = string.IsNullOrWhiteSpace(request.SizeId)
            ? null
            : menu.KichCos.SingleOrDefault(x => x.MaKichCo == request.SizeId);
        if (!string.IsNullOrWhiteSpace(request.SizeId) && size is null)
            throw new ApiException(400, "The selected size does not belong to this menu item.");
        if (string.IsNullOrWhiteSpace(request.SizeId) && menu.KichCos.Count > 0)
            throw new ApiException(400, "Select a size offered by this menu item.");
        if (request.Toppings.Select(x => x.ToppingId).Distinct().Count() != request.Toppings.Count)
            throw new ApiException(400, "Duplicate toppings are not allowed.");
        if (request.Toppings.Any(x => x.Quantity != 1))
            throw new ApiException(400, "The ERD topping association does not store a topping quantity.");

        var selectedToppings = request.Toppings.Select(selected =>
            menu.Toppings.SingleOrDefault(x => x.MaTopping == selected.ToppingId)
            ?? throw new ApiException(400, "The topping does not belong to this menu item.")).ToList();
        var basePrice = size?.GiaBan ?? menu.KichCos.Min(x => x.GiaBan);
        var unitPrice = basePrice + selectedToppings.Sum(x => x.GiaBan);
        ServiceHelpers.Money(unitPrice);
        var line = new ChiTietDonHang
        {
            MaChiTiet = await ids.ChiTietAsync(ct),
            MaDon = order.MaDon,
            DonHang = order,
            MaMon = menu.MaMon,
            Mon = menu,
            MaKichCo = size?.MaKichCo,
            KichCo = size,
            SoLuong = request.Quantity,
            DonGia = unitPrice,
            GhiChu = request.Note?.Trim() ?? string.Empty,
            TrangThaiMon = OrderItemStatus.Waiting
        };
        foreach (var topping in selectedToppings)
            line.ChiTietToppings.Add(new ChiTietTopping
            {
                MaChiTiet = line.MaChiTiet,
                ChiTiet = line,
                MaTopping = topping.MaTopping,
                Topping = topping
            });
        return line;
    }

    private static ChiTietDonHang FindLine(DonHang order, string id) =>
        order.ChiTiets.SingleOrDefault(x => x.MaChiTiet == id) ?? throw ServiceHelpers.NotFound("Order item");

    private static decimal Subtotal(DonHang order) =>
        order.ChiTiets.Where(x => x.TrangThaiMon != OrderItemStatus.Cancelled)
            .Sum(x => x.DonGia * x.SoLuong);

    private static void RequireActive(DonHang order)
    {
        if (order.TrangThai is OrderStatus.Paid or OrderStatus.Cancelled)
            throw ServiceHelpers.Conflict("Paid or cancelled orders cannot be changed.");
    }

    private static void RequireMutable(DonHang order)
    {
        if (order.TrangThai is not (OrderStatus.Open or OrderStatus.Confirmed or OrderStatus.Preparing))
            throw ServiceHelpers.Conflict("Completed orders cannot be changed.");
    }

    private static void RequireMergeable(DonHang order)
    {
        if (order.TrangThai is not (OrderStatus.Open or OrderStatus.Confirmed or OrderStatus.Preparing))
            throw ServiceHelpers.Conflict("Only open, uncompleted orders can be transferred or merged.");
    }

    private static void RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ApiException(400, "An operation reason is required.");
    }

    private static string? CancelledItemId(string auditContent)
    {
        const string marker = "Line ";
        var start = auditContent.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return null;
        start += marker.Length;
        var end = auditContent.IndexOf(' ', start);
        return end > start ? auditContent[start..end] : null;
    }

    private static void MoveActiveTableLinks(DonHang source, DonHang target)
    {
        var now = DateTime.UtcNow;
        var links = source.DonHangBans.Where(link => link.ThoiGianKetThuc is null).ToList();
        foreach (var link in links)
        {
            link.ThoiGianKetThuc = now;
            AddActiveTableLink(target, link.Ban);
        }
    }

    private static void AddActiveTableLink(DonHang order, Ban table)
    {
        if (order.DonHangBans.Any(link => link.MaBan == table.MaBan && link.ThoiGianKetThuc is null))
            return;
        var now = DateTime.UtcNow;
        order.DonHangBans.Add(new DonHangBan
        {
            MaDon = order.MaDon,
            DonHang = order,
            MaBan = table.MaBan,
            Ban = table,
            ThoiGianBatDau = now
        });
    }

    private static void ValidateMergeableTable(Ban table)
    {
        if (table.TrangThai is TableStatus.Disabled or TableStatus.NeedsCleaning)
            throw ServiceHelpers.Conflict("Only available or occupied tables can be merged.");
    }

    private static void ValidateMergeOrders(DonHang source, DonHang target)
    {
        RequireMergeable(source);
        RequireMergeable(target);
        if (source.LoaiDon != OrderType.DineIn || target.LoaiDon != OrderType.DineIn)
            throw new ApiException(400, "Table merging requires two dine-in orders.");
        if ((source.TrangThai == OrderStatus.Open) != (target.TrangThai == OrderStatus.Open))
            throw ServiceHelpers.Conflict("Confirm both orders or leave both as drafts before merging.");
        if (source.ChiTiets.Count + target.ChiTiets.Count > 100)
            throw new ApiException(400, "The merged order would exceed 100 lines.");
    }

    private static void MoveOrderLines(DonHang source, DonHang target)
    {
        foreach (var line in source.ChiTiets.ToList())
        {
            line.MaDon = target.MaDon;
            line.DonHang = target;
            target.ChiTiets.Add(line);
        }
        source.ChiTiets.Clear();
    }

    private void ValidateOrderToken(DonHang? order, string? expected)
    {
        if (order is null)
        {
            if (!string.IsNullOrEmpty(expected))
                throw ServiceHelpers.Conflict("A table's active order changed. Reload the floor plan.");
            return;
        }
        if (string.IsNullOrEmpty(expected))
            throw new ApiException(400, "The active order RowVersion is required.");
        ServiceHelpers.Version(db.Entry(order), order.RowVersion, expected);
    }

    private static void RefreshOrderStatus(DonHang order)
    {
        var active = order.ChiTiets.Where(x => x.TrangThaiMon != OrderItemStatus.Cancelled).ToList();
        if (active.Count == 0)
        {
            order.TrangThai = OrderStatus.Cancelled;
            ServiceHelpers.CloseTableLinks(order, TableStatus.NeedsCleaning);
        }
        else if (order.TrangThai != OrderStatus.Open)
        {
            order.TrangThai = active.Any(x =>
                x.TrangThaiMon is OrderItemStatus.Preparing or OrderItemStatus.Completed or OrderItemStatus.Served)
                ? OrderStatus.Preparing
                : OrderStatus.Confirmed;
        }
    }

    private async Task SaveAsync(DonHang order, string action, string description, CancellationToken ct)
    {
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), action, "DonHang", order.MaDon, description);
        await db.SaveChangesAsync(ct);
    }

    private async Task<OrderResponse> MapAsync(DonHang order, CancellationToken ct)
    {
        var owners = await PreparedOwnersAsync(order.ChiTiets, ct);
        return Map(order, owners);
    }

    private async Task<List<OrderResponse>> MapManyAsync(IReadOnlyCollection<DonHang> orders, CancellationToken ct)
    {
        var owners = await PreparedOwnersAsync(orders.SelectMany(order => order.ChiTiets), ct);
        return orders.Select(order => Map(order, owners)).ToList();
    }

    private async Task<Dictionary<string, string>> PreparedOwnersAsync(
        IEnumerable<ChiTietDonHang> source, CancellationToken ct)
    {
        var lines = source.ToList();
        if (lines.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var auditTargets = lines.Select(line => $"ChiTietDonHang:{line.MaChiTiet}").ToArray();
        var claims = await db.NhatKyThaoTacs.AsNoTracking()
            .Where(x => x.LoaiThaoTac == "ItemClaimed" && auditTargets.Contains(x.DoiTuongTacDong))
            .OrderBy(x => x.ThoiGian)
            .Select(x => new { x.DoiTuongTacDong, x.MaNhanVien })
            .ToListAsync(ct);
        var latestClaims = claims.GroupBy(x => x.DoiTuongTacDong, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().MaNhanVien, StringComparer.Ordinal);
        if (latestClaims.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var employeeIds = latestClaims.Values.Distinct(StringComparer.Ordinal).ToArray();
        var accounts = await db.TaiKhoans.AsNoTracking()
            .Where(account => employeeIds.Contains(account.MaNhanVien))
            .Select(account => new { account.MaNhanVien, account.TenDangNhap })
            .ToListAsync(ct);
        var accountIds = accounts.ToDictionary(account => account.MaNhanVien, account => account.TenDangNhap,
            StringComparer.Ordinal);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (latestClaims.TryGetValue($"ChiTietDonHang:{line.MaChiTiet}", out var employeeId) &&
                accountIds.TryGetValue(employeeId, out var userId))
                result[line.MaChiTiet] = userId;
        }
        return result;
    }

    private static OrderResponse Map(DonHang x, IReadOnlyDictionary<string, string> owners) => new(
        x.MaDon, x.MaDon, x.LoaiDon, x.MaBan, x.Ban?.MaBan, x.TrangThai,
        x.MaNhanVien, new DateTimeOffset(DateTime.SpecifyKind(x.ThoiGianTao, DateTimeKind.Utc)),
        null, x.TamTinh, Convert.ToBase64String(x.RowVersion), x.ChiTiets.OrderBy(i => i.MaChiTiet)
            .Select(item => MapLineWithOwner(item, owners.GetValueOrDefault(item.MaChiTiet))).ToList());

    internal static OrderItemResponse MapLine(ChiTietDonHang x) => MapLineWithOwner(x, null);

    private static OrderItemResponse MapLineWithOwner(ChiTietDonHang x, string? preparedByUserId) => new(
        x.MaChiTiet, x.MaMon, x.Mon?.TenMon ?? x.MaMon, x.MaKichCo, x.KichCo?.TenKichCo,
        x.SoLuong, x.DonGia, x.DonGia * x.SoLuong, string.IsNullOrEmpty(x.GhiChu) ? null : x.GhiChu,
        x.TrangThaiMon, preparedByUserId, Convert.ToBase64String(x.RowVersion),
        x.ChiTietToppings.Select(t => new OrderToppingResponse(
            t.MaTopping, t.Topping?.TenTopping ?? t.MaTopping, 1, t.Topping?.GiaBan ?? 0m)).ToList());

    private async Task AddOrderNoticesAsync(
        DonHang order, IEnumerable<string> roleNames, string message, CancellationToken ct)
    {
        var table = order.Ban?.MaBan ?? order.MaBan ?? "Mang đi";
        await AddNoticesAsync(roleNames, order, $"{order.MaDon} ({table}): {message}", "OrderChanged", ct);
    }

    private async Task AddNoticesAsync(
        IEnumerable<string> roleNames, DonHang? order, string content, string type, CancellationToken ct)
    {
        foreach (var roleName in roleNames.Distinct(StringComparer.Ordinal))
            await AddNoticeAsync(roleName, order, content, type, ct);
    }

    private async Task<ThongBao> AddNoticeAsync(
        string roleName, DonHang? order, string content, string type, CancellationToken ct)
    {
        var role = await db.VaiTros.SingleOrDefaultAsync(x => x.TenVaiTro == roleName, ct)
            ?? throw new ApiException(500, $"The {roleName} role is not configured.");
        var notice = new ThongBao
        {
            MaThongBao = await ids.ThongBaoAsync(ct),
            MaDon = order?.MaDon,
            DonHang = order,
            MaVaiTro = role.MaVaiTro,
            VaiTro = role,
            NoiDung = content,
            LoaiThongBao = type,
            ThoiGian = DateTime.UtcNow
        };
        db.ThongBaos.Add(notice);
        return notice;
    }

    private static KitchenNoticeResponse MapNotice(ThongBao notice, bool isRead)
    {
        var separator = notice.NoiDung.IndexOf(": ", StringComparison.Ordinal);
        var entityId = notice.MaDon ??
            (separator >= 0 ? notice.NoiDung[..separator] : null);
        var description = separator >= 0 ? notice.NoiDung[(separator + 2)..] : notice.NoiDung;
        return new(notice.MaThongBao, null, entityId, description, isRead,
            new DateTimeOffset(DateTime.SpecifyKind(notice.ThoiGian, DateTimeKind.Utc)));
    }
}

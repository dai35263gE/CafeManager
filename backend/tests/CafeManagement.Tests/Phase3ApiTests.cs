using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Enums;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CafeManagement.Tests;

[Collection("Phase2 HTTP")]
public sealed class Phase3ApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [SqlServerFact]
    public async Task ConcurrentPaymentCreatesOneInvoiceAndCancelledInvoicesNeverCountAsRevenue()
    {
        await using var host = await SqlApiHost.CreateAsync("Phase3");
        using var waiter = await Login(host, "waiter");
        using var manager = await Login(host, "manager");
        using var cashier = await Login(host, "cashier");
        var menu = (await Data<PageResponse<MenuItemResponse>>(await waiter.GetAsync("/api/mon"))).Items.First(x => x.Sizes.Count > 0);
        var recipe = Assert.Single((await Data<PageResponse<RecipeResponse>>(
            await manager.GetAsync($"/api/dinh-luong?menuItemId={menu.Id}"))).Items);
        var ingredientBefore = await Data<IngredientResponse>(await manager.GetAsync($"/api/nguyen-lieu/{recipe.MaNguyenLieu}"));
        var table = (await Data<PageResponse<TableResponse>>(await waiter.GetAsync("/api/ban"))).Items.First();
        var order = await ServedOrder(waiter, manager, menu, table.Id);
        var promotion = await Data<PromotionResponse>(await manager.PostAsJsonAsync("/api/khuyen-mai", new
        {
            code = "TEST10", name = "Test discount", startAt = DateTimeOffset.UtcNow.AddDays(-1),
            endAt = DateTimeOffset.UtcNow.AddDays(1), type = "Percentage", discountValue = 10
        }));
        var preview = await Data<PromotionValidationResponse>(await cashier.PostAsJsonAsync("/api/khuyen-mai/validate",
            new { orderId = order.Id, code = "test10" }));
        Assert.Equal(order.SubTotal / 10, preview.DiscountAmount);
        Assert.Equal(HttpStatusCode.BadRequest, (await cashier.PostAsJsonAsync("/api/hoa-don", new
        {
            orderId = order.Id, rowVersion = order.RowVersion, paymentMethod = "Cash", receivedAmount = 0, promotionCode = promotion.Code
        })).StatusCode);
        Assert.Equal("Served", (await Data<OrderResponse>(await waiter.GetAsync($"/api/don-hang/{order.Id}"))).Status.ToString());
        var body = new { orderId = order.Id, rowVersion = order.RowVersion, paymentMethod = "Cash",
            receivedAmount = preview.TotalAmount + 10000, promotionCode = promotion.Code };
        var attempts = await Task.WhenAll(cashier.PostAsJsonAsync("/api/hoa-don", body), manager.PostAsJsonAsync("/api/hoa-don", body));
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Conflict);
        var invoice = await Data<InvoiceResponse>(attempts.Single(x => x.StatusCode == HttpStatusCode.Created));
        Assert.NotNull(attempts.Single(x => x.StatusCode == HttpStatusCode.Created).Headers.Location);
        Assert.Equal(preview.TotalAmount, invoice.TotalAmount);
        Assert.Equal(10000, invoice.ChangeAmount);
        var ingredientAfter = await Data<IngredientResponse>(await manager.GetAsync($"/api/nguyen-lieu/{recipe.MaNguyenLieu}"));
        Assert.Equal(ingredientBefore.Quantity - recipe.SoLuongSuDung * 2, ingredientAfter.Quantity);
        var consumption = await Data<PageResponse<InventoryTransactionResponse>>(
            await manager.GetAsync($"/api/nguyen-lieu/{recipe.MaNguyenLieu}/history"));
        Assert.Contains(consumption.Items, item => item.Type == InventoryTransactionType.Export &&
            item.Reason.Contains(invoice.Id, StringComparison.Ordinal));
        var usage = await Data<PromotionUsageSummaryResponse>(
            await manager.GetAsync($"/api/khuyen-mai/{promotion.Id}/history/summary"));
        Assert.Equal(1, usage.UsageCount);
        Assert.Equal(invoice.DiscountAmount, usage.TotalDiscountAmount);
        var usageHistory = await Data<PageResponse<InvoiceResponse>>(
            await manager.GetAsync($"/api/khuyen-mai/{promotion.Id}/history"));
        Assert.Single(usageHistory.Items);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.GetAsync($"/api/khuyen-mai/{promotion.Id}/history")).StatusCode);
        Assert.Equal("Paid", (await Data<OrderResponse>(await waiter.GetAsync($"/api/don-hang/{order.Id}"))).Status.ToString());
        Assert.Equal("NeedsCleaning", (await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{table.Id}"))).Status.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await waiter.GetAsync($"/api/hoa-don/{invoice.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await waiter.PostAsJsonAsync($"/api/don-hang/{order.Id}/items", new
        {
            rowVersion = (await Data<OrderResponse>(await waiter.GetAsync($"/api/don-hang/{order.Id}"))).RowVersion,
            items = new[] { new { menuItemId = menu.Id, sizeId = menu.Sizes[0].SizeId, quantity = 1 } }
        })).StatusCode);
        var range = Range();
        var revenue = await Data<RevenueReportResponse>(await manager.GetAsync("/api/bao-cao/revenue?" + range));
        Assert.Equal(1, revenue.InvoiceCount);
        Assert.Equal(invoice.TotalAmount, revenue.TotalRevenue);
        Assert.Equal(invoice.DiscountAmount, revenue.TotalDiscount);
        var top = await Data<PageResponse<TopItemResponse>>(await manager.GetAsync("/api/bao-cao/top-items?" + range));
        Assert.Equal(2, top.Items.Single().Quantity);
        Assert.Equal(order.SubTotal, top.Items.Single().GrossRevenue);
        var methods = await Data<PageResponse<PaymentMethodResponse>>(await manager.GetAsync("/api/bao-cao/payment-methods?" + range));
        Assert.Equal(invoice.TotalAmount, methods.Items.Single().Revenue);
        var revenueDetails = await Data<PageResponse<ReportInvoiceResponse>>(await manager.GetAsync(
            $"/api/bao-cao/details?report=revenue&key={invoice.PaidAt:yyyy-MM-dd}&{range}"));
        var itemDetails = await Data<PageResponse<ReportInvoiceResponse>>(await manager.GetAsync(
            $"/api/bao-cao/details?report=top-items&key={top.Items.Single().MenuItemId}&{range}"));
        var paymentDetails = await Data<PageResponse<ReportInvoiceResponse>>(await manager.GetAsync(
            $"/api/bao-cao/details?report=payment-methods&key={invoice.PaymentMethod}&{range}"));
        Assert.All(new[] { revenueDetails, itemDetails, paymentDetails }, details =>
            Assert.Contains(details.Items, x => x.Id == invoice.Id && x.TotalAmount == invoice.TotalAmount));
        Assert.Equal(invoice.PaidAt.UtcDateTime, DateTime.SpecifyKind(
            revenueDetails.Items.Single().PaidAt, DateTimeKind.Utc));
        Assert.Equal(invoice.Id, (await Data<InvoiceResponse>(
            await manager.GetAsync($"/api/hoa-don/{revenueDetails.Items.Single().Id}"))).Id);
        using var csv = await manager.GetAsync("/api/bao-cao/export/revenue?" + range);
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(csv.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Contains(invoice.TotalAmount.ToString(System.Globalization.CultureInfo.InvariantCulture), await csv.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/bao-cao/export/top-items?" + range)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/bao-cao/export/payment-methods?" + range)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync(
            "/api/bao-cao/export/top-items?" + range + "&search=missing-menu-item")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync(
            $"/api/bao-cao/revenue?from={Uri.EscapeDataString(invoice.PaidAt.ToString("O"))}&to={Uri.EscapeDataString(invoice.PaidAt.ToString("O"))}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync("/api/bao-cao/revenue?from=" +
            Uri.EscapeDataString(invoice.PaidAt.AddDays(-1).ToString("O")) + "&to=" +
            Uri.EscapeDataString(invoice.PaidAt.ToString("O")))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsJsonAsync($"/api/hoa-don/{invoice.Id}/cancel",
            new { rowVersion = invoice.RowVersion, reason = "Not permitted" })).StatusCode);
        var cancelled = await Data<InvoiceResponse>(await manager.PostAsJsonAsync($"/api/hoa-don/{invoice.Id}/cancel",
            new { rowVersion = invoice.RowVersion, reason = "Duplicate customer charge correction" }));
        Assert.Equal(InvoiceStatus.Cancelled, cancelled.Status);
        Assert.Equal(invoice.TotalAmount, cancelled.TotalAmount);
        usage = await Data<PromotionUsageSummaryResponse>(
            await manager.GetAsync($"/api/khuyen-mai/{promotion.Id}/history/summary"));
        Assert.Equal(0, usage.UsageCount);
        Assert.Equal(0, usage.TotalDiscountAmount);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync("/api/bao-cao/revenue?" + range)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync("/api/bao-cao/top-items?" + range)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync("/api/bao-cao/payment-methods?" + range)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await cashier.PostAsJsonAsync("/api/hoa-don", body)).StatusCode);
        Assert.Equal(1, (await Data<PageResponse<InvoiceResponse>>(await cashier.GetAsync("/api/hoa-don"))).TotalCount);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CafeDbContext>();
        var stored = await db.HoaDons.SingleAsync(x => x.MaHoaDon == invoice.Id);
        stored.TongThanhToan = -1;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task PromotionRulesNoncashPaymentsAndImmutableInvoiceSnapshotsWork()
    {
        await using var host = await SqlApiHost.CreateAsync("Phase3");
        using var waiter = await Login(host, "waiter");
        using var manager = await Login(host, "manager");
        using var cashier = await Login(host, "cashier");
        var menu = (await Data<PageResponse<MenuItemResponse>>(await waiter.GetAsync("/api/mon"))).Items.First(x => x.Sizes.Count > 0);
        var draft = await Data<OrderResponse>(await waiter.PostAsJsonAsync("/api/don-hang", new
        {
            type = "TakeAway", items = new[] { new { menuItemId = menu.Id, sizeId = menu.Sizes[0].SizeId, quantity = 1 } }
        }));
        Assert.Equal(HttpStatusCode.Conflict, (await cashier.PostAsJsonAsync("/api/hoa-don", new
        {
            orderId = draft.Id, rowVersion = draft.RowVersion, paymentMethod = "Cash", receivedAmount = 100000
        })).StatusCode);
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync("/api/khuyen-mai", new
        {
            code = "BAD", name = "Bad percent", startAt = now.AddDays(-1), endAt = now.AddDays(1),
            type = "Percentage", discountValue = 101
        })).StatusCode);
        var expired = await Data<PromotionResponse>(await manager.PostAsJsonAsync("/api/khuyen-mai", new
        {
            code = "EXPIRED", name = "Old", startAt = now.AddDays(-2), endAt = now.AddDays(-1),
            type = "FixedAmount", discountValue = 1000
        }));
        Assert.Equal(PromotionStatus.Expired, expired.Status);
        Assert.Contains((await Data<PageResponse<PromotionResponse>>(await manager.GetAsync("/api/khuyen-mai?status=Expired"))).Items,
            x => x.Id == expired.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await cashier.PostAsJsonAsync("/api/khuyen-mai/validate",
            new { orderId = draft.Id, code = expired.Code })).StatusCode);
        var big = await Data<PromotionResponse>(await manager.PostAsJsonAsync("/api/khuyen-mai", new
        {
            code = "FREE", name = "Large fixed amount", startAt = now.AddDays(-1), endAt = now.AddDays(1),
            type = "FixedAmount", discountValue = 999999
        }));
        var preview = await Data<PromotionValidationResponse>(await cashier.PostAsJsonAsync("/api/khuyen-mai/validate",
            new { orderId = draft.Id, code = big.Code }));
        Assert.Equal(0, preview.TotalAmount);
        Assert.Equal(draft.SubTotal, preview.DiscountAmount);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/api/khuyen-mai/{big.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cashier.PostAsJsonAsync("/api/khuyen-mai/validate",
            new { orderId = draft.Id, code = big.Code })).StatusCode);
        var order = await ServedOrder(waiter, manager, menu);
        Assert.Equal(HttpStatusCode.BadRequest, (await cashier.PostAsJsonAsync("/api/hoa-don", new
        {
            orderId = order.Id, rowVersion = order.RowVersion, paymentMethod = "Card", receivedAmount = order.SubTotal + 1
        })).StatusCode);
        var invoice = await Data<InvoiceResponse>(await cashier.PostAsJsonAsync("/api/hoa-don", new
        {
            orderId = order.Id, rowVersion = order.RowVersion, paymentMethod = "Card", receivedAmount = order.SubTotal
        }));
        Assert.Equal(0, invoice.ChangeAmount);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CafeDbContext>();
        var stored = await db.HoaDons.SingleAsync(x => x.MaHoaDon == invoice.Id);
        Assert.Equal(invoice.TotalAmount, stored.TongThanhToan);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/api/bao-cao/revenue?from=2026-01-02T00%3A00%3A00Z&to=2026-01-01T00%3A00%3A00Z")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/api/bao-cao/revenue")).StatusCode);
    }

    [SqlServerFact]
    public async Task InvoiceQuoteIncludesConfiguredTaxAndFeeAndCancellationReturnsAuditMetadata()
    {
        await using var host = await SqlApiHost.CreateAsync("Billing", new Dictionary<string, string?>
        {
            ["Billing:VatRatePercent"] = "10",
            ["Billing:ServiceFeeRatePercent"] = "5"
        });
        using var waiter = await Login(host, "waiter");
        using var manager = await Login(host, "manager");
        using var cashier = await Login(host, "cashier");
        var menu = (await Data<PageResponse<MenuItemResponse>>(await waiter.GetAsync("/api/mon")))
            .Items.First(x => x.Sizes.Count > 0);
        var order = await ServedOrder(waiter, manager, menu);
        var promotion = await Data<PromotionResponse>(await manager.PostAsJsonAsync("/api/khuyen-mai", new
        {
            code = "BILL10", name = "Billing test discount", startAt = DateTimeOffset.UtcNow.AddDays(-1),
            endAt = DateTimeOffset.UtcNow.AddDays(1), type = "Percentage", discountValue = 10
        }));

        var quote = await Data<InvoiceQuoteResponse>(await cashier.GetAsync(
            $"/api/hoa-don/quote?orderId={Uri.EscapeDataString(order.Id)}" +
            $"&rowVersion={Uri.EscapeDataString(order.RowVersion)}&promotionCode={Uri.EscapeDataString(promotion.Code)}"));
        var expectedDiscount = decimal.Round(order.SubTotal * 0.10m, 2, MidpointRounding.AwayFromZero);
        var afterDiscount = order.SubTotal - expectedDiscount;
        var expectedVat = decimal.Round(afterDiscount * 0.10m, 2, MidpointRounding.AwayFromZero);
        var expectedFee = decimal.Round(afterDiscount * 0.05m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedDiscount, quote.DiscountAmount);
        Assert.Equal(expectedVat, quote.VatAmount);
        Assert.Equal(expectedFee, quote.ServiceFeeAmount);
        Assert.Equal(afterDiscount + expectedVat + expectedFee, quote.TotalAmount);
        Assert.Equal(promotion.Code, quote.PromotionCode);

        var configuration = host.Services.GetRequiredService<IConfiguration>();
        configuration["Billing:VatRatePercent"] = "not-a-rate";
        var fallbackQuote = await Data<InvoiceQuoteResponse>(await cashier.GetAsync(
            $"/api/hoa-don/quote?orderId={Uri.EscapeDataString(order.Id)}" +
            $"&rowVersion={Uri.EscapeDataString(order.RowVersion)}&promotionCode={Uri.EscapeDataString(promotion.Code)}"));
        Assert.Equal(0, fallbackQuote.VatRatePercent);
        Assert.Equal(0, fallbackQuote.VatAmount);
        Assert.Contains(fallbackQuote.Warnings, warning => warning.Contains("thuế VAT", StringComparison.Ordinal));
        configuration["Billing:VatRatePercent"] = "10";

        var invoice = await Data<InvoiceResponse>(await cashier.PostAsJsonAsync("/api/hoa-don", new
        {
            orderId = order.Id, rowVersion = order.RowVersion, promotionCode = promotion.Code,
            paymentMethod = "Cash", receivedAmount = quote.TotalAmount
        }));
        Assert.Equal(quote.VatAmount, invoice.VatAmount);
        Assert.Equal(quote.ServiceFeeAmount, invoice.ServiceFeeAmount);
        Assert.Equal(quote.TotalAmount, invoice.TotalAmount);
        Assert.Equal(8, Convert.FromBase64String(invoice.RowVersion).Length);

        var listed = await Data<PageResponse<InvoiceResponse>>(await cashier.GetAsync("/api/hoa-don"));
        Assert.Equal(invoice.RowVersion, Assert.Single(listed.Items).RowVersion);
        var printed = await cashier.GetStringAsync($"/api/hoa-don/{invoice.Id}/print");
        var vietnameseCulture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        Assert.Contains($"Thue VAT: {invoice.VatAmount.ToString("N0", vietnameseCulture)}", printed);
        Assert.Contains($"Phi dich vu: {invoice.ServiceFeeAmount.ToString("N0", vietnameseCulture)}", printed);

        var staleBytes = Convert.FromBase64String(invoice.RowVersion);
        staleBytes[0] ^= 0xff;
        var staleToken = Convert.ToBase64String(staleBytes);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(
            $"/api/hoa-don/{invoice.Id}/cancel", new { rowVersion = staleToken, reason = "Wrong token" })).StatusCode);

        var managerId = (await Data<UserResponse>(await manager.GetAsync("/api/auth/me"))).EmployeeId;
        var reason = "Customer payment correction";
        var cancelled = await Data<InvoiceResponse>(await manager.PostAsJsonAsync(
            $"/api/hoa-don/{invoice.Id}/cancel", new { rowVersion = invoice.RowVersion, reason }));
        Assert.Equal(InvoiceStatus.Cancelled, cancelled.Status);
        Assert.Equal(reason, cancelled.CancelReason);
        Assert.Equal(managerId, cancelled.CancelledByUserId);
        Assert.NotNull(cancelled.CancelledAt);
        Assert.Equal(invoice.TotalAmount, cancelled.TotalAmount);

        Assert.Equal(HttpStatusCode.Conflict, (await cashier.GetAsync(
            $"/api/hoa-don/quote?orderId={Uri.EscapeDataString(order.Id)}" +
            $"&rowVersion={Uri.EscapeDataString(order.RowVersion)}")).StatusCode);
    }

    [SqlServerFact]
    public async Task PromotionLifecycleLocksStartedTermsAndSupportsUsageSummaryForUnusedCodes()
    {
        await using var host = await SqlApiHost.CreateAsync("PromotionLifecycle");
        using var manager = await Login(host, "manager");
        using var cashier = await Login(host, "cashier");
        using var waiter = await Login(host, "waiter");
        var now = DateTimeOffset.UtcNow;
        var tomorrow = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        var futureRequest = new
        {
            code = "FUTURE7", name = "Scheduled promotion",
            startAt = now.AddDays(2), endAt = now.AddDays(5),
            type = "Percentage", discountValue = 10, minOrderAmount = 10000
        };
        var future = await Data<PromotionResponse>(await manager.PostAsJsonAsync("/api/khuyen-mai", futureRequest));
        Assert.Equal("Scheduled", future.EffectiveStatus);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync("/api/khuyen-mai", futureRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync("/api/khuyen-mai", new
        {
            code = "BADDATE7", name = "Invalid date range",
            startAt = tomorrow, endAt = tomorrow.AddHours(1),
            type = "Percentage", discountValue = 10
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync("/api/khuyen-mai", new
        {
            code = "TOOLONGCODE7", name = "Invalid code",
            startAt = now.AddDays(2), endAt = now.AddDays(5),
            type = "Percentage", discountValue = 10
        })).StatusCode);
        var changedFuture = await Data<PromotionResponse>(await manager.PutAsJsonAsync(
            $"/api/khuyen-mai/{future.Id}", new
            {
                futureRequest.code, name = "Changed before start", futureRequest.startAt, futureRequest.endAt,
                futureRequest.type, discountValue = 15, futureRequest.minOrderAmount
            }));
        Assert.Equal(15, changedFuture.DiscountValue);

        var runningRequest = new
        {
            code = "RUNNING7", name = "Running promotion",
            startAt = now.AddDays(-1), endAt = now.AddDays(3),
            type = "FixedAmount", discountValue = 5000, minOrderAmount = 0
        };
        var running = await Data<PromotionResponse>(await manager.PostAsJsonAsync("/api/khuyen-mai", runningRequest));
        Assert.Equal("Active", running.EffectiveStatus);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PutAsJsonAsync(
            $"/api/khuyen-mai/{running.Id}", new
            {
                runningRequest.code, name = "Changed terms", runningRequest.startAt, runningRequest.endAt,
                type = "FixedAmount", discountValue = 6000, runningRequest.minOrderAmount
            })).StatusCode);
        var renamed = await Data<PromotionResponse>(await manager.PutAsJsonAsync(
            $"/api/khuyen-mai/{running.Id}", new
            {
                runningRequest.code, name = "Renamed running promotion", runningRequest.startAt, runningRequest.endAt,
                runningRequest.type, runningRequest.discountValue, runningRequest.minOrderAmount
            }));
        Assert.Equal("Renamed running promotion", renamed.Name);

        Assert.Equal(HttpStatusCode.NoContent,
            (await manager.DeleteAsync($"/api/khuyen-mai/{running.Id}")).StatusCode);
        var paused = await Data<PromotionResponse>(await manager.GetAsync($"/api/khuyen-mai/{running.Id}"));
        Assert.Equal(PromotionStatus.Inactive, paused.Status);
        var resumed = await Data<PromotionResponse>(await manager.PatchAsJsonAsync(
            $"/api/khuyen-mai/{running.Id}/resume", new { }));
        Assert.Equal(PromotionStatus.Active, resumed.Status);
        Assert.Equal(HttpStatusCode.NoContent,
            (await manager.DeleteAsync($"/api/khuyen-mai/{running.Id}")).StatusCode);
        var ended = await Data<PromotionResponse>(await manager.PatchAsJsonAsync(
            $"/api/khuyen-mai/{running.Id}/end", new { }));
        Assert.Equal(PromotionStatus.Expired, ended.Status);
        Assert.Equal("Ended", ended.EffectiveStatus);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PatchAsJsonAsync(
            $"/api/khuyen-mai/{running.Id}/end", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PatchAsJsonAsync(
            $"/api/khuyen-mai/{running.Id}/resume", new { })).StatusCode);

        var summary = await Data<PromotionUsageSummaryResponse>(
            await manager.GetAsync($"/api/khuyen-mai/{future.Id}/history/summary"));
        Assert.Equal(0, summary.UsageCount);
        Assert.Equal(0, summary.TotalDiscountAmount);
        Assert.Empty((await Data<PageResponse<InvoiceResponse>>(
            await manager.GetAsync($"/api/khuyen-mai/{future.Id}/history"))).Items);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.GetAsync($"/api/khuyen-mai/{future.Id}/history/summary")).StatusCode);

        var menu = (await Data<PageResponse<MenuItemResponse>>(await waiter.GetAsync("/api/mon")))
            .Items.First(x => x.Sizes.Count > 0);
        var order = await Data<OrderResponse>(await waiter.PostAsJsonAsync("/api/don-hang", new
        {
            type = "TakeAway", items = new[] { new { menuItemId = menu.Id, sizeId = menu.Sizes[0].SizeId, quantity = 1 } }
        }));
        Assert.Equal(HttpStatusCode.BadRequest, (await cashier.PostAsJsonAsync(
            "/api/khuyen-mai/validate", new { orderId = order.Id, code = running.Code })).StatusCode);
    }

    [SqlServerFact]
    public async Task InventoryHistoryStocktakeWarningsAndConcurrentExportsRemainConsistent()
    {
        await using var host = await SqlApiHost.CreateAsync("Phase3");
        using var warehouse = await Login(host, "warehouse");
        using var barista = await Login(host, "barista");
        using var cashier = await Login(host, "cashier");
        var ingredient = await Data<IngredientResponse>(await warehouse.PostAsJsonAsync("/api/nguyen-lieu", new
        {
            code = "TESTSTOCK", name = "=HYPERLINK(\"bad\")", unit = "kg", minQuantity = 5,
            expiryDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)
        }));
        Assert.Equal(0, ingredient.Quantity);
        var import = await Data<InventoryMovementResponse>(await warehouse.PostAsJsonAsync($"/api/nguyen-lieu/{ingredient.Id}/import",
            new { quantity = 10, reason = "Opening stock", rowVersion = ingredient.RowVersion }));
        ingredient = import.Ingredient;
        Assert.Equal(0, import.Transaction.PreviousQuantity);
        Assert.Equal(10, import.Transaction.ResultingQuantity);
        var attempts = await Task.WhenAll(
            warehouse.PostAsJsonAsync($"/api/nguyen-lieu/{ingredient.Id}/export", new { quantity = 7, reason = "A", rowVersion = ingredient.RowVersion }),
            warehouse.PostAsJsonAsync($"/api/nguyen-lieu/{ingredient.Id}/export", new { quantity = 7, reason = "B", rowVersion = ingredient.RowVersion }));
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Conflict);
        ingredient = await Data<IngredientResponse>(await warehouse.GetAsync($"/api/nguyen-lieu/{ingredient.Id}"));
        Assert.Equal(3, ingredient.Quantity);
        Assert.Equal(HttpStatusCode.BadRequest, (await warehouse.PostAsJsonAsync($"/api/nguyen-lieu/{ingredient.Id}/export",
            new { quantity = 4, reason = "Too much", rowVersion = ingredient.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await warehouse.PostAsJsonAsync($"/api/nguyen-lieu/{ingredient.Id}/adjust",
            new { quantity = -4, reason = "Negative", rowVersion = ingredient.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await barista.PostAsJsonAsync($"/api/nguyen-lieu/{ingredient.Id}/import",
            new { quantity = 1, reason = "Not allowed", rowVersion = ingredient.RowVersion })).StatusCode);
        var stocktake = await Data<InventoryMovementResponse>(await warehouse.PostAsJsonAsync($"/api/nguyen-lieu/{ingredient.Id}/stocktake",
            new { quantity = 2.5m, reason = "Physical count", rowVersion = ingredient.RowVersion }));
        var adjust = await Data<InventoryMovementResponse>(await warehouse.PostAsJsonAsync($"/api/nguyen-lieu/{ingredient.Id}/adjust",
            new { quantity = -0.5m, reason = "Spillage", rowVersion = stocktake.Ingredient.RowVersion }));
        ingredient = adjust.Ingredient;
        Assert.Equal(2, ingredient.Quantity);
        var history = await Data<PageResponse<InventoryTransactionResponse>>(await warehouse.GetAsync($"/api/nguyen-lieu/{ingredient.Id}/history"));
        Assert.Equal(4, history.TotalCount);
        Assert.All(history.Items, x => Assert.False(string.IsNullOrWhiteSpace(x.PerformedByUserId)));
        Assert.Equal(HttpStatusCode.OK, (await warehouse.GetAsync($"/api/nguyen-lieu/{ingredient.Id}/history/{import.Transaction.Id}")).StatusCode);
        var warnings = await Data<PageResponse<IngredientResponse>>(await barista.GetAsync("/api/nguyen-lieu/warnings"));
        Assert.Contains(warnings.Items, x => x.Id == ingredient.Id && x.IsLowStock && x.IsExpired);
        Assert.Equal(HttpStatusCode.Conflict, (await warehouse.PutAsJsonAsync($"/api/nguyen-lieu/{ingredient.Id}", new
        {
            code = ingredient.Code, name = ingredient.Name, unit = "litre", minQuantity = 5, rowVersion = ingredient.RowVersion
        })).StatusCode);
        var noChange = await Data<InventoryMovementResponse>(await warehouse.PostAsJsonAsync($"/api/nguyen-lieu/{ingredient.Id}/stocktake",
            new { quantity = 2, reason = "Recount", rowVersion = ingredient.RowVersion }));
        Assert.NotEqual(ingredient.RowVersion, noChange.Ingredient.RowVersion);
        using var csv = await warehouse.GetAsync("/api/bao-cao/export/inventory");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        var bytes = await csv.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Take(3).SequenceEqual(Encoding.UTF8.GetPreamble()));
        Assert.Contains("'=HYPERLINK", Encoding.UTF8.GetString(bytes));
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/api/bao-cao/inventory")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await warehouse.GetAsync("/api/bao-cao/export/revenue?" + Range())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await warehouse.DeleteAsync(
            $"/api/nguyen-lieu/{ingredient.Id}?rowVersion={Uri.EscapeDataString(noChange.Ingredient.RowVersion)}")).StatusCode);
        var retained = await Data<IngredientResponse>(await warehouse.GetAsync($"/api/nguyen-lieu/{ingredient.Id}"));
        Assert.Equal(2, retained.Quantity);
        Assert.Equal(4, (await Data<PageResponse<InventoryTransactionResponse>>(await warehouse.GetAsync($"/api/nguyen-lieu/{ingredient.Id}/history"))).TotalCount);
        var report = await Data<PageResponse<IngredientResponse>>(await warehouse.GetAsync("/api/bao-cao/inventory?search=TESTSTOCK"));
        Assert.Equal(2, report.Items.Single().Quantity);
    }

    [SqlServerFact]
    public async Task InventoryWarningsIncludeThresholdAndOutOfStock()
    {
        await using var host = await SqlApiHost.CreateAsync("Phase3");
        using var warehouse = await Login(host, "warehouse");
        var ingredient = await Data<IngredientResponse>(await warehouse.PostAsJsonAsync("/api/nguyen-lieu", new
        {
            code = "TESTALERT", name = "Alert ingredient", unit = "kg", minQuantity = 5,
            expiryDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5)
        }));
        var import = await Data<InventoryMovementResponse>(await warehouse.PostAsJsonAsync(
            $"/api/nguyen-lieu/{ingredient.Id}/import",
            new { quantity = 5, reason = "Threshold check", rowVersion = ingredient.RowVersion }));
        var atThreshold = await Data<PageResponse<IngredientResponse>>(await warehouse.GetAsync("/api/nguyen-lieu/warnings?search=TESTALERT"));
        Assert.Contains(atThreshold.Items, x => x.IsLowStock && !x.IsOutOfStock);
        var lowStockReport = await Data<PageResponse<IngredientResponse>>(await warehouse.GetAsync(
            "/api/bao-cao/inventory?filter=low-stock&search=TESTALERT"));
        Assert.Contains(lowStockReport.Items, x => x.IsLowStock);
        Assert.Equal(8, Convert.FromBase64String(Assert.Single(lowStockReport.Items).RowVersion).Length);
        var expiryReport = await Data<PageResponse<IngredientResponse>>(await warehouse.GetAsync(
            "/api/bao-cao/inventory?filter=expiring&search=TESTALERT"));
        Assert.Contains(expiryReport.Items, x => x.IsExpiringSoon);
        using var filteredCsv = await warehouse.GetAsync("/api/bao-cao/export/inventory?filter=expiring&search=TESTALERT");
        Assert.Contains("TESTALERT", await filteredCsv.Content.ReadAsStringAsync());
        var stocktake = await Data<InventoryMovementResponse>(await warehouse.PostAsJsonAsync(
            $"/api/nguyen-lieu/{ingredient.Id}/stocktake",
            new { quantity = 0, reason = "Out of stock check", rowVersion = import.Ingredient.RowVersion }));
        Assert.True(stocktake.Ingredient.IsOutOfStock);
        var urgent = await Data<PageResponse<IngredientResponse>>(await warehouse.GetAsync("/api/nguyen-lieu/warnings?search=TESTALERT"));
        Assert.Contains(urgent.Items, x => x.IsOutOfStock && x.IsLowStock);
    }

    [SqlServerFact]
    public async Task PaymentRequiresRecipesAndSufficientIngredientStock()
    {
        await using var host = await SqlApiHost.CreateAsync("Phase3");
        using var waiter = await Login(host, "waiter");
        using var manager = await Login(host, "manager");
        using var cashier = await Login(host, "cashier");
        var menu = (await Data<PageResponse<MenuItemResponse>>(await waiter.GetAsync("/api/mon"))).Items.First(x => x.Sizes.Count > 0);
        var table = (await Data<PageResponse<TableResponse>>(await waiter.GetAsync("/api/ban"))).Items.First();
        var order = await ServedOrder(waiter, manager, menu, table.Id);
        var recipes = await Data<PageResponse<RecipeResponse>>(
            await manager.GetAsync($"/api/dinh-luong?menuItemId={menu.Id}"));
        var recipe = Assert.Single(recipes.Items);
        Assert.Equal(HttpStatusCode.NoContent,
            (await manager.DeleteAsync($"/api/dinh-luong/{recipe.MaDinhLuong}")).StatusCode);
        var payment = new
        {
            orderId = order.Id, rowVersion = order.RowVersion,
            paymentMethod = "Cash", receivedAmount = order.SubTotal + 10000
        };
        Assert.Equal(HttpStatusCode.Conflict, (await cashier.PostAsJsonAsync("/api/hoa-don", payment)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync("/api/dinh-luong", new
        {
            maMon = menu.Id, maNguyenLieu = recipe.MaNguyenLieu, soLuongSuDung = recipe.SoLuongSuDung
        })).StatusCode);
        var ingredient = await Data<IngredientResponse>(await manager.GetAsync($"/api/nguyen-lieu/{recipe.MaNguyenLieu}"));
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync(
            $"/api/nguyen-lieu/{ingredient.Id}/stocktake",
            new { quantity = 0, reason = "Prepare insufficient stock test", rowVersion = ingredient.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await cashier.PostAsJsonAsync("/api/hoa-don", payment)).StatusCode);
        var invoices = await Data<PageResponse<InvoiceResponse>>(await cashier.GetAsync($"/api/hoa-don?search={order.Id}"));
        Assert.Empty(invoices.Items);
        Assert.Equal(0, (await Data<IngredientResponse>(
            await manager.GetAsync($"/api/nguyen-lieu/{recipe.MaNguyenLieu}"))).Quantity);
    }

    [SqlServerFact]
    public async Task RecipeCreationRejectsDuplicateMenuIngredientPairs()
    {
        await using var host = await SqlApiHost.CreateAsync("Phase3");
        using var manager = await Login(host, "manager");
        var recipes = await Data<PageResponse<RecipeResponse>>(await manager.GetAsync("/api/dinh-luong"));
        var existing = Assert.IsType<RecipeResponse>(recipes.Items.FirstOrDefault());
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync("/api/dinh-luong", new
        {
            maMon = existing.MaMon, maNguyenLieu = existing.MaNguyenLieu, soLuongSuDung = 1
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PutAsJsonAsync($"/api/dinh-luong/{existing.MaDinhLuong}", new
        {
            maMon = existing.MaMon, maNguyenLieu = existing.MaNguyenLieu, soLuongSuDung = 0
        })).StatusCode);
    }

    [SqlServerFact]
    public async Task EmployeeResignationLocksLinkedAccountWithoutAllowingRoleEscalation()
    {
        await using var host = await SqlApiHost.CreateAsync("Phase3");
        using var manager = await Login(host, "manager");
        using var admin = await Login(host, "admin");
        var employee = await Data<EmployeeResponse>(await manager.PostAsJsonAsync("/api/nhan-vien", new
        {
            fullName = "Test staff", position = "Waiter", phone = "+84987654321", email = "staff-uc9@cafe.example"
        }));
        Assert.StartsWith("NV", employee.Code);
        Assert.Equal("Working", employee.Status.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync("/api/nhan-vien", new
        {
            fullName = "Duplicate contact", position = "Cashier", email = "staff-uc9@cafe.example"
        })).StatusCode);
        var matches = await Data<PageResponse<EmployeeResponse>>(await manager.GetAsync(
            "/api/nhan-vien?search=Test&position=Waiter&status=Working"));
        Assert.Contains(matches.Items, x => x.Id == employee.Id);
        Assert.Equal(employee.Id, (await Data<EmployeeResponse>(
            await manager.GetAsync($"/api/nhan-vien/{employee.Id}"))).Id);

        var managerProfile = await Data<UserResponse>(await manager.GetAsync("/api/auth/me"));
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PatchAsJsonAsync(
            $"/api/nhan-vien/{managerProfile.EmployeeId}/account/lock", new { locked = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync(
            $"/api/nhan-vien/{employee.Id}/account", new { username = "staff", password = "short", role = "Waiter" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync(
            $"/api/nhan-vien/{employee.Id}/account", new { username = "staff", password = "Staff@123", role = "Admin" })).StatusCode);
        var account = await Data<UserResponse>(await manager.PostAsJsonAsync(
            $"/api/nhan-vien/{employee.Id}/account", new { username = "staff", password = "Staff@123", role = "Waiter" }));
        Assert.Equal("Waiter", account.Role);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/tai-khoan", new
        {
            username = "staff", password = "Other@123", fullName = "Duplicate user",
            email = "duplicate-user@cafe.example", role = "Waiter"
        })).StatusCode);
        using var staff = await Login(host, "staff", "Staff@123");
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/don-hang")).StatusCode);
        var locked = await Data<UserResponse>(await manager.PatchAsJsonAsync(
            $"/api/nhan-vien/{employee.Id}/account/lock", new { locked = true }));
        Assert.True(locked.IsLocked);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/auth/me")).StatusCode);
        Assert.True((await Data<EmployeeResponse>(await manager.GetAsync($"/api/nhan-vien/{employee.Id}"))).AccountIsLocked);
        await manager.PatchAsJsonAsync($"/api/nhan-vien/{employee.Id}/account/lock", new { locked = false });
        using var returned = await Login(host, "staff", "Staff@123");
        var resigned = await Data<EmployeeResponse>(await manager.PutAsJsonAsync($"/api/nhan-vien/{employee.Id}", new
        {
            code = employee.Code, fullName = employee.FullName, position = employee.Position, status = "Resigned"
        }));
        Assert.Equal(account.Id, resigned.UserId);
        Assert.Contains((await Data<PageResponse<EmployeeResponse>>(await admin.GetAsync("/api/nhan-vien?status=Resigned"))).Items,
            x => x.Id == employee.Id && x.UserId == account.Id);
        Assert.False(resigned.AccountIsActive);
        Assert.Equal(HttpStatusCode.Unauthorized, (await returned.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync($"/api/tai-khoan/{account.Id}/lock", new { locked = false })).StatusCode);
        employee = await Data<EmployeeResponse>(await manager.PutAsJsonAsync($"/api/nhan-vien/{employee.Id}", new
        {
            code = employee.Code, fullName = employee.FullName, position = employee.Position, status = "Working"
        }));
        Assert.False(employee.AccountIsActive);
        Assert.Equal(HttpStatusCode.OK, (await manager.PatchAsJsonAsync(
            $"/api/nhan-vien/{employee.Id}/account/lock", new { locked = false })).StatusCode);
        using var reactivated = await Login(host, "staff", "Staff@123");
        Assert.Equal(HttpStatusCode.OK, (await reactivated.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/api/nhan-vien/{employee.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await reactivated.GetAsync("/api/auth/me")).StatusCode);
        var deleted = await Data<EmployeeResponse>(await manager.GetAsync($"/api/nhan-vien/{employee.Id}"));
        Assert.Equal(EmployeeStatus.Resigned, deleted.Status);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/tai-khoan/{account.Id}")).StatusCode);
    }

    private static async Task<OrderResponse> ServedOrder(HttpClient waiter, HttpClient manager, MenuItemResponse menu, string? tableId = null)
    {
        var order = await Data<OrderResponse>(await waiter.PostAsJsonAsync("/api/don-hang", new
        {
            type = tableId is not null ? "DineIn" : "TakeAway", tableId,
            items = new[] { new { menuItemId = menu.Id, sizeId = menu.Sizes[0].SizeId, quantity = 2 } }
        }));
        order = await Data<OrderResponse>(await waiter.PatchAsJsonAsync($"/api/don-hang/{order.Id}/status",
            new { status = "Confirmed", rowVersion = order.RowVersion }));
        foreach (var status in new[] { "Preparing", "Completed", "Served" })
            order = await Data<OrderResponse>(await manager.PatchAsJsonAsync($"/api/don-hang/{order.Id}/items/{order.Items.Single().Id}/status",
                new { status, rowVersion = order.Items.Single().RowVersion }));
        order = await Data<OrderResponse>(await waiter.PatchAsJsonAsync($"/api/don-hang/{order.Id}/status",
            new { status = "Served", rowVersion = order.RowVersion }));
        return order;
    }

    private static string Range() => $"from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}" +
        $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"))}";

    private static async Task<HttpClient> Login(SqlApiHost host, string username, string? password = null)
    {
        var client = host.CreateClient();
        password ??= char.ToUpperInvariant(username[0]) + username[1..] + "@123";
        var login = await Data<LoginResponse>(await client.PostAsJsonAsync("/api/auth/login", new { username, password }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        return client;
    }

    private static async Task<T> Data<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        var result = JsonSerializer.Deserialize<ApiResponse<T>>(body, Json);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        return result.Data;
    }
}

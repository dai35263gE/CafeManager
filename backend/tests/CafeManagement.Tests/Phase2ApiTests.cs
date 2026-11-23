using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CafeManagement.Api.Contracts;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ApiHost = CafeManagement.Tests.SqlApiHost;

namespace CafeManagement.Tests;

[CollectionDefinition("Phase2 HTTP", DisableParallelization = true)]
public sealed class Phase2Collection;

[Collection("Phase2 HTTP")]
public sealed class Phase2ApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [SqlServerFact]
    public async Task AuthRolesAccountChangesAndPasswordChangesInvalidateTokens()
    {
        await using var host = await ApiHost.CreateAsync();
        using var anonymous = host.CreateClient();
        using var unauthorized = await anonymous.GetAsync("/api/don-hang");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        await AssertError(unauthorized);
        using var invalid = await anonymous.PostAsJsonAsync("/api/auth/login", new { username = "waiter", password = "Wrong@123" });
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        using var waiter = await SignIn(host, "waiter");
        using var admin = await SignIn(host, "admin");
        using var manager = await SignIn(host, "manager");
        using var barista = await SignIn(host, "barista");
        using var warehouse = await SignIn(host, "warehouse");
        using var cashier = await SignIn(host, "cashier");
        Assert.Equal(HttpStatusCode.Forbidden, (await warehouse.GetAsync("/api/don-hang")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/mon")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await waiter.GetAsync("/api/tai-khoan")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await barista.PostAsJsonAsync("/api/don-hang", new { type = "TakeAway" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsJsonAsync("/api/don-hang", new { type = "TakeAway" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/tai-khoan")).StatusCode);
        var account = await Data<UserResponse>(await waiter.GetAsync("/api/auth/me"));
        var changeRole = await admin.PutAsJsonAsync($"/api/tai-khoan/{account.Id}", new
        {
            fullName = account.FullName, email = "waiter@cafe.example", role = "Barista"
        });
        Assert.Equal(HttpStatusCode.OK, changeRole.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await waiter.GetAsync("/api/auth/me")).StatusCode);
        using var changed = await SignIn(host, "waiter");
        Assert.Equal("Barista", (await Data<UserResponse>(await changed.GetAsync("/api/auth/me"))).Role);
        Assert.Equal(HttpStatusCode.Forbidden, (await changed.PostAsJsonAsync("/api/don-hang", new { type = "TakeAway" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/tai-khoan/{account.Id}/lock", new { locked = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await changed.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login",
            new { username = "waiter", password = "Waiter@123" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/tai-khoan/{account.Id}/lock", new { locked = false })).StatusCode);
        using var unlocked = await SignIn(host, "waiter");
        Assert.Equal(HttpStatusCode.OK, (await unlocked.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "Waiter@123", newPassword = "Updated@123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await unlocked.GetAsync("/api/auth/me")).StatusCode);
        var fresh = await Data<LoginResponse>(await anonymous.PostAsJsonAsync("/api/auth/login",
            new { username = "waiter", password = "Updated@123" }));
        Assert.False(string.IsNullOrWhiteSpace(fresh.Token));
        var adminAccount = await Data<UserResponse>(await admin.GetAsync("/api/auth/me"));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PatchAsJsonAsync(
            $"/api/tai-khoan/{adminAccount.Id}/lock", new { locked = true })).StatusCode);
        using var swagger = await anonymous.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        var document = JsonDocument.Parse(await swagger.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/don-hang/{id}/merge", out _));
        Assert.Equal("bearer", document.RootElement.GetProperty("components")
            .GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CafeDbContext>();
        Assert.True(await db.NhatKyThaoTacs.AnyAsync(x => x.LoaiThaoTac == "PasswordChanged"));
        Assert.True(await db.NhatKyThaoTacs.AnyAsync(x => x.LoaiThaoTac == "Login"));
    }

    [SqlServerFact]
    public async Task AdminCanManageOperationsAndCompleteAnotherBaristasItem()
    {
        await using var host = await ApiHost.CreateAsync();
        using var admin = await SignIn(host, "admin");
        using var barista = await SignIn(host, "barista");
        foreach (var path in new[]
        {
            "/api/ban", "/api/mon", "/api/don-hang", "/api/hoa-don", "/api/khuyen-mai",
            "/api/nguyen-lieu", "/api/dinh-luong", "/api/nhan-vien", "/api/bao-cao/inventory",
            "/api/tai-khoan"
        })
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(path)).StatusCode);

        var employee = await Data<EmployeeResponse>(await admin.PostAsJsonAsync("/api/nhan-vien",
            new { fullName = "Admin-created employee", position = "Waiter" }));
        Assert.False(string.IsNullOrWhiteSpace(employee.Id));
        var menu = (await Data<PageResponse<MenuItemResponse>>(await admin.GetAsync("/api/mon"))).Items
            .Single(x => x.Name == "Cà phê sữa");
        var order = await Data<OrderResponse>(await admin.PostAsJsonAsync("/api/don-hang", new
        {
            type = "TakeAway", confirm = true,
            items = new[] { new { menuItemId = menu.Id, sizeId = menu.Sizes.First().SizeId, quantity = 1 } }
        }));
        var item = order.Items.Single();
        order = await Data<OrderResponse>(await barista.PatchAsJsonAsync(
            $"/api/don-hang/{order.Id}/items/{item.Id}/status",
            new { status = "Preparing", rowVersion = item.RowVersion }));
        order = await Data<OrderResponse>(await admin.PatchAsJsonAsync(
            $"/api/don-hang/{order.Id}/items/{item.Id}/status",
            new { status = "Completed", rowVersion = order.Items.Single().RowVersion }));
        Assert.Equal("Completed", order.Items.Single().Status.ToString());
        var notice = (await Data<PageResponse<KitchenNoticeResponse>>(
            await barista.GetAsync("/api/don-hang/kitchen-notices"))).Items.First();
        Assert.True((await Data<KitchenNoticeResponse>(await admin.PatchAsync(
            $"/api/don-hang/kitchen-notices/{notice.Id}/read", null))).IsRead);
        order = await Data<OrderResponse>(await admin.PatchAsJsonAsync(
            $"/api/don-hang/{order.Id}/status",
            new { status = "Cancelled", rowVersion = order.RowVersion, reason = "Admin cancellation" }));
        Assert.Equal("Cancelled", order.Status.ToString());
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/mon/{menu.Id}/status",
            new { status = "OutOfStock", rowVersion = menu.RowVersion })).StatusCode);
    }

    [SqlServerFact]
    public async Task OrderSnapshotsKitchenOwnershipCancellationsAndAvailabilityWork()
    {
        await using var host = await ApiHost.CreateAsync();
        using var waiter = await SignIn(host, "waiter");
        using var manager = await SignIn(host, "manager");
        using var barista = await SignIn(host, "barista");
        using var cashier = await SignIn(host, "cashier");
        using var admin = await SignIn(host, "admin");
        var secondAccount = await Data<UserResponse>(await admin.PostAsJsonAsync("/api/tai-khoan", new
        {
            username = "barista2", password = "Barista@123", fullName = "Second barista",
            email = "barista2@cafe.example", role = "Barista"
        }));
        Assert.False(string.IsNullOrWhiteSpace(secondAccount.Id));
        using var secondBarista = await SignIn(host, "barista2", "Barista@123");
        var menu = (await Data<PageResponse<MenuItemResponse>>(await waiter.GetAsync("/api/mon"))).Items
            .Single(x => x.Name == "Cà phê sữa");
        var table = (await Data<PageResponse<TableResponse>>(await waiter.GetAsync("/api/ban"))).Items.First();
        var topping = menu.Toppings.First();
        var size = menu.Sizes.First();
        var empty = await Data<OrderResponse>(await waiter.PostAsJsonAsync("/api/don-hang",
            new { type = "TakeAway", items = Array.Empty<object>() }));
        Assert.Equal(HttpStatusCode.Conflict, (await waiter.PatchAsJsonAsync($"/api/don-hang/{empty.Id}/status",
            new { status = "Confirmed", rowVersion = empty.RowVersion })).StatusCode);
        var response = await waiter.PostAsJsonAsync("/api/don-hang", new
        {
            type = "DineIn", tableId = table.Id, items = new[]
            {
                new { menuItemId = menu.Id, sizeId = size.SizeId, quantity = 2,
                    note = "Ít đá",
                    toppings = new[] { new { toppingId = topping.Id, quantity = 1 } } }
            }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var order = await Data<OrderResponse>(response);
        var originalPrice = size.Price + topping.Price;
        Assert.Equal(originalPrice * 2, order.SubTotal);
        Assert.Equal("Ít đá", order.Items.Single().Note);
        Assert.Equal(HttpStatusCode.BadRequest, (await waiter.PostAsJsonAsync("/api/don-hang",
            new { type = "TakeAway", confirm = true, items = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await waiter.PostAsJsonAsync("/api/don-hang",
            new { type = "DineIn", tableId = table.Id })).StatusCode);
        var occupied = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{table.Id}"));
        Assert.Equal(HttpStatusCode.Conflict, (await waiter.PatchAsJsonAsync($"/api/ban/{table.Id}/status",
            new { status = "Available", rowVersion = occupied.RowVersion })).StatusCode);
        var queueBefore = await Data<PageResponse<KitchenItemResponse>>(await barista.GetAsync("/api/don-hang/kitchen-queue"));
        Assert.DoesNotContain(queueBefore.Items, x => x.OrderId == order.Id);
        var priceUpdate = await manager.PutAsJsonAsync($"/api/mon/{menu.Id}", new
        {
            categoryId = menu.CategoryId, name = menu.Name, basePrice = menu.BasePrice + 10000,
            sizes = menu.Sizes.Select(x => new { sizeId = x.SizeId, price = x.Price + 10000 }),
            toppingIds = menu.Toppings.Select(x => x.Id), status = "Available", isActive = true,
            rowVersion = menu.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, priceUpdate.StatusCode);
        menu = await Data<MenuItemResponse>(priceUpdate);
        order = await Data<OrderResponse>(await waiter.PatchAsJsonAsync($"/api/don-hang/{order.Id}/status",
            new { status = "Confirmed", rowVersion = order.RowVersion }));
        var confirmedNotice = (await Data<PageResponse<KitchenNoticeResponse>>(
            await barista.GetAsync("/api/don-hang/kitchen-notices"))).Items
            .Single(x => x.Description.Contains("chờ pha chế"));
        Assert.False(confirmedNotice.IsRead);
        Assert.True((await Data<KitchenNoticeResponse>(await barista.PatchAsync(
            $"/api/don-hang/kitchen-notices/{confirmedNotice.Id}/read", null))).IsRead);
        Assert.True((await Data<PageResponse<KitchenNoticeResponse>>(
            await barista.GetAsync("/api/don-hang/kitchen-notices"))).Items
            .Single(x => x.Id == confirmedNotice.Id).IsRead);
        Assert.Equal(HttpStatusCode.NotFound, (await waiter.PatchAsync(
            $"/api/don-hang/kitchen-notices/{confirmedNotice.Id}/read", null)).StatusCode);
        var item = order.Items.Single();
        order = await Data<OrderResponse>(await waiter.PatchAsJsonAsync(
            $"/api/don-hang/{order.Id}/items/{item.Id}",
            new { quantity = item.Quantity, note = "Ít đường", rowVersion = item.RowVersion }));
        item = order.Items.Single();
        Assert.Equal("Ít đường", item.Note);
        var firstClaim = barista.PatchAsJsonAsync($"/api/don-hang/{order.Id}/items/{item.Id}/status",
            new { status = "Preparing", rowVersion = item.RowVersion });
        var secondClaim = secondBarista.PatchAsJsonAsync($"/api/don-hang/{order.Id}/items/{item.Id}/status",
            new { status = "Preparing", rowVersion = item.RowVersion });
        var claims = await Task.WhenAll(firstClaim, secondClaim);
        Assert.Single(claims, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(claims, x => x.StatusCode == HttpStatusCode.Conflict);
        order = await Data<OrderResponse>(await waiter.GetAsync($"/api/don-hang/{order.Id}"));
        item = order.Items.Single();
        var baristaId = (await Data<UserResponse>(await barista.GetAsync("/api/auth/me"))).Id;
        var owner = item.PreparedByUserId == baristaId ? barista : secondBarista;
        var other = ReferenceEquals(owner, barista) ? secondBarista : barista;
        Assert.Contains((await Data<PageResponse<KitchenNoticeResponse>>(
            await barista.GetAsync("/api/don-hang/kitchen-notices"))).Items,
            x => x.Description.Contains("Preparing") && x.Description.Contains(menu.Name));
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PatchAsJsonAsync(
            $"/api/don-hang/{order.Id}/items/{item.Id}/status", new { status = "Completed", rowVersion = item.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await waiter.DeleteWithJson(
            $"/api/don-hang/{order.Id}/items/{item.Id}", new { rowVersion = item.RowVersion, reason = "Changed mind" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await waiter.PatchAsJsonAsync(
            $"/api/don-hang/{order.Id}/items/{item.Id}", new { quantity = 3, rowVersion = item.RowVersion })).StatusCode);
        order = await Data<OrderResponse>(await owner.PatchAsJsonAsync(
            $"/api/don-hang/{order.Id}/items/{item.Id}/status", new { status = "Completed", rowVersion = item.RowVersion }));
        Assert.Contains((await Data<PageResponse<KitchenNoticeResponse>>(
            await waiter.GetAsync("/api/don-hang/kitchen-notices"))).Items,
            x => x.Description.Contains("Completed") && x.Description.Contains(menu.Name));
        var completed = await Data<PageResponse<KitchenItemResponse>>(await waiter.GetAsync("/api/don-hang/kitchen-queue?status=Completed"));
        Assert.Contains(completed.Items, x => x.Item.Id == item.Id);
        order = await Data<OrderResponse>(await waiter.PatchAsJsonAsync(
            $"/api/don-hang/{order.Id}/items/{item.Id}/status",
            new { status = "Served", rowVersion = order.Items.Single().RowVersion }));
        Assert.Equal("Preparing", order.Status.ToString());
        Assert.Equal(originalPrice * 2, order.SubTotal);
        order = await Data<OrderResponse>(await waiter.PostAsJsonAsync($"/api/don-hang/{order.Id}/items", new
        {
            rowVersion = order.RowVersion,
            items = new[] { new { menuItemId = menu.Id, sizeId = size.SizeId, quantity = 1 } }
        }));
        Assert.Equal(originalPrice * 2 + size.Price + 10000, order.SubTotal);
        Assert.Equal(HttpStatusCode.Conflict, (await waiter.PatchAsJsonAsync($"/api/don-hang/{order.Id}/status",
            new { status = "Served", rowVersion = order.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await waiter.PatchAsJsonAsync($"/api/don-hang/{order.Id}/status",
            new { status = "Paid", rowVersion = order.RowVersion })).StatusCode);
        var waiting = order.Items.Single(x => x.Status.ToString() == "Waiting");
        Assert.Equal(HttpStatusCode.NoContent, (await waiter.DeleteWithJson($"/api/don-hang/{order.Id}/items/{waiting.Id}",
            new { rowVersion = waiting.RowVersion, reason = "Duplicate line" })).StatusCode);
        order = await Data<OrderResponse>(await waiter.GetAsync($"/api/don-hang/{order.Id}"));
        var cancelledLine = order.Items.Single(x => x.Id == waiting.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await waiter.PatchAsJsonAsync(
            $"/api/don-hang/{order.Id}/items/{waiting.Id}/reopen",
            new { rowVersion = cancelledLine.RowVersion, reason = "Customer changed their mind" })).StatusCode);
        order = await Data<OrderResponse>(await manager.PatchAsJsonAsync(
            $"/api/don-hang/{order.Id}/items/{waiting.Id}/reopen",
            new { rowVersion = cancelledLine.RowVersion, reason = "Customer confirmed the order" }));
        Assert.Equal("Waiting", order.Items.Single(x => x.Id == waiting.Id).Status.ToString());
        var reopenedLine = order.Items.Single(x => x.Id == waiting.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteWithJson(
            $"/api/don-hang/{order.Id}/items/{waiting.Id}",
            new { rowVersion = reopenedLine.RowVersion, reason = "Customer changed their mind" })).StatusCode);
        order = await Data<OrderResponse>(await waiter.GetAsync($"/api/don-hang/{order.Id}"));
        Assert.Equal(originalPrice * 2, order.SubTotal);
        order = await Data<OrderResponse>(await waiter.PatchAsJsonAsync($"/api/don-hang/{order.Id}/status",
            new { status = "Served", rowVersion = order.RowVersion }));
        Assert.Equal("Served", order.Status.ToString());
        Assert.Contains((await Data<PageResponse<KitchenNoticeResponse>>(
            await cashier.GetAsync("/api/don-hang/kitchen-notices"))).Items,
            x => x.Description.Contains("sẵn sàng thanh toán"));
        Assert.Equal(HttpStatusCode.Conflict, (await waiter.PostAsJsonAsync($"/api/don-hang/{order.Id}/items", new
        {
            rowVersion = order.RowVersion,
            items = new[] { new { menuItemId = menu.Id, sizeId = size.SizeId, quantity = 1 } }
        })).StatusCode);
        var cancellation = await manager.DeleteWithJson($"/api/don-hang/{order.Id}",
            new { rowVersion = order.RowVersion, reason = "Manager cancellation" });
        Assert.True(cancellation.StatusCode == HttpStatusCode.NoContent,
            $"Order cancellation returned {(int)cancellation.StatusCode}: {await cancellation.Content.ReadAsStringAsync()}");
        var cancelled = await Data<PageResponse<KitchenItemResponse>>(await barista.GetAsync("/api/don-hang/kitchen-queue?status=Cancelled"));
        Assert.Contains(cancelled.Items, x => x.OrderId == order.Id);
        Assert.Equal("NeedsCleaning", (await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{table.Id}"))).Status.ToString());
        var affectedOrder = await Data<OrderResponse>(await waiter.PostAsJsonAsync("/api/don-hang", new
        {
            type = "TakeAway", confirm = true,
            items = new[] { new { menuItemId = menu.Id, sizeId = size.SizeId, quantity = 1 } }
        }));
        var shortage = await barista.PostAsJsonAsync("/api/don-hang/shortage-reports", new { menuItemId = menu.Id, reason = "No milk" });
        Assert.Equal(HttpStatusCode.Created, shortage.StatusCode);
        Assert.Contains((await Data<PageResponse<KitchenNoticeResponse>>(await waiter.GetAsync("/api/don-hang/kitchen-notices"))).Items,
            x => x.Description.Contains("No milk") && x.Description.Contains(affectedOrder.Id) &&
                x.Description.Contains("barista") && x.Description.Contains("thời điểm"));
        Assert.Equal(HttpStatusCode.Conflict, (await barista.PostAsJsonAsync(
            "/api/don-hang/shortage-reports", new { menuItemId = menu.Id, reason = "Duplicate report" })).StatusCode);
        Assert.DoesNotContain((await Data<PageResponse<KitchenNoticeResponse>>(
            await cashier.GetAsync("/api/don-hang/kitchen-notices"))).Items,
            x => x.Description.Contains("No milk"));
        var availableMenu = await Data<PageResponse<MenuItemResponse>>(await waiter.GetAsync("/api/mon"));
        Assert.DoesNotContain(availableMenu.Items, x => x.Id == menu.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await waiter.PostAsJsonAsync("/api/don-hang",
            new { type = "TakeAway", items = new[] { new { menuItemId = menu.Id, sizeId = size.SizeId, quantity = 1 } } })).StatusCode);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CafeDbContext>();
            var ingredientId = await db.NguyenLieus.Select(x => x.MaNguyenLieu).FirstAsync();
            Assert.Equal(HttpStatusCode.Created, (await barista.PostAsJsonAsync(
                "/api/don-hang/shortage-reports", new { ingredientId })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await barista.PatchAsJsonAsync($"/api/mon/{menu.Id}/status",
            new { status = "Discontinued" })).StatusCode);
        menu = await Data<MenuItemResponse>(await waiter.GetAsync($"/api/mon/{menu.Id}"));
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync(
            $"/api/mon/{menu.Id}?rowVersion={Uri.EscapeDataString(menu.RowVersion)}")).StatusCode);
        Assert.Equal("Discontinued", (await Data<MenuItemResponse>(await manager.GetAsync($"/api/mon/{menu.Id}"))).Status.ToString());
        Assert.Equal(menu.Name, (await Data<OrderResponse>(await waiter.GetAsync($"/api/don-hang/{order.Id}"))).Items[0].MenuItemName);
    }

    [SqlServerFact]
    public async Task KitchenQueueUsesItemCallTimeAndShowsSelectionDetails()
    {
        await using var host = await ApiHost.CreateAsync();
        using var waiter = await SignIn(host, "waiter");
        using var barista = await SignIn(host, "barista");
        var menu = (await Data<PageResponse<MenuItemResponse>>(await waiter.GetAsync("/api/mon"))).Items
            .Single(x => x.Name == "Cà phê sữa");
        var size = menu.Sizes.First();
        var topping = menu.Toppings.First();
        var firstOrder = await Data<OrderResponse>(await waiter.PostAsJsonAsync("/api/don-hang", new
        {
            type = "TakeAway", confirm = true,
            items = new[] { new
            {
                menuItemId = menu.Id, sizeId = size.SizeId, quantity = 1, note = "Ít đá",
                toppings = new[] { new { toppingId = topping.Id, quantity = 1 } }
            } }
        }));
        var firstItem = firstOrder.Items.Single();
        var secondOrder = await Data<OrderResponse>(await waiter.PostAsJsonAsync("/api/don-hang", new
        {
            type = "TakeAway", confirm = true,
            items = new[] { new { menuItemId = menu.Id, sizeId = size.SizeId, quantity = 1 } }
        }));
        var secondItem = secondOrder.Items.Single();
        firstOrder = await Data<OrderResponse>(await waiter.PostAsJsonAsync(
            $"/api/don-hang/{firstOrder.Id}/items", new
            {
                rowVersion = firstOrder.RowVersion,
                items = new[] { new { menuItemId = menu.Id, sizeId = size.SizeId, quantity = 1 } }
            }));
        var addedItem = firstOrder.Items.Single(x => x.Id != firstItem.Id);
        var queue = await Data<PageResponse<KitchenItemResponse>>(
            await barista.GetAsync("/api/don-hang/kitchen-queue?status=Waiting"));
        Assert.Equal(new[] { firstItem.Id, secondItem.Id, addedItem.Id },
            queue.Items.Select(x => x.Item.Id).Take(3));
        var details = queue.Items.Single(x => x.Item.Id == firstItem.Id).Item;
        Assert.Equal("Ít đá", details.Note);
        Assert.Equal(size.Name, details.SizeName);
        Assert.Equal(topping.Name, details.Toppings.Single().Name);
        Assert.Equal(1, details.Toppings.Single().Quantity);
    }

    [SqlServerFact]
    public async Task TransferMergeStaleTokensAndConcurrentTableCreationAreAtomic()
    {
        await using var host = await ApiHost.CreateAsync();
        using var waiter = await SignIn(host, "waiter");
        var tables = (await Data<PageResponse<TableResponse>>(await waiter.GetAsync("/api/ban"))).Items;
        var source = await Data<OrderResponse>(await waiter.PostAsJsonAsync("/api/don-hang", new { type = "DineIn", tableId = tables[0].Id }));
        var menu = (await Data<PageResponse<MenuItemResponse>>(await waiter.GetAsync("/api/mon"))).Items.First(x => x.Sizes.Count > 0);
        var newLine = new { menuItemId = menu.Id, sizeId = menu.Sizes[0].SizeId, quantity = 1 };
        var oldToken = source.RowVersion;
        source = await Data<OrderResponse>(await waiter.PostAsJsonAsync($"/api/don-hang/{source.Id}/items",
            new { rowVersion = oldToken, items = new[] { newLine } }));
        Assert.Equal(HttpStatusCode.Conflict, (await waiter.PostAsJsonAsync($"/api/don-hang/{source.Id}/items",
            new { rowVersion = oldToken, items = new[] { newLine } })).StatusCode);
        var sourceTable = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{tables[0].Id}"));
        var targetTable = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{tables[1].Id}"));
        source = await Data<OrderResponse>(await waiter.PostAsJsonAsync("/api/ban/transfer", new
        {
            sourceTableId = tables[0].Id, targetTableId = tables[1].Id,
            orderRowVersion = source.RowVersion, sourceTableRowVersion = sourceTable.RowVersion,
            targetTableRowVersion = targetTable.RowVersion
        }));
        Assert.Equal(tables[1].Id, source.TableId);
        var released = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{tables[0].Id}"));
        Assert.Equal("NeedsCleaning", released.Status.ToString());
        Assert.Equal(HttpStatusCode.OK, (await waiter.PatchAsJsonAsync($"/api/ban/{released.Id}/status",
            new { status = "Available", rowVersion = released.RowVersion })).StatusCode);
        var target = await Data<OrderResponse>(await waiter.PostAsJsonAsync("/api/don-hang",
            new { type = "DineIn", tableId = tables[2].Id, items = new[] { newLine } }));
        var sourceLine = source.Items.Single().Id;
        var subtotal = source.SubTotal + target.SubTotal;
        sourceTable = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{tables[1].Id}"));
        targetTable = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{tables[2].Id}"));
        var merge = await Data<MergeTablesResponse>(await waiter.PostAsJsonAsync("/api/ban/merge", new
        {
            targetTableId = tables[2].Id,
            targetTableRowVersion = targetTable.RowVersion,
            targetActiveOrderRowVersion = target.RowVersion,
            targetOrderId = target.Id,
            targetOrderRowVersion = target.RowVersion,
            sources = new[] { new
            {
                tableId = tables[1].Id,
                tableRowVersion = sourceTable.RowVersion,
                activeOrderRowVersion = source.RowVersion
            } }
        }));
        target = merge.ActiveOrder!;
        Assert.Equal(target.Id, merge.ActiveOrder?.Id);
        Assert.Equal(new[] { tables[1].Id }, merge.SourceTableIds);
        Assert.Equal(subtotal, target.SubTotal);
        Assert.Contains(target.Items, x => x.Id == sourceLine);
        var merged = await Data<OrderResponse>(await waiter.GetAsync($"/api/don-hang/{source.Id}"));
        Assert.Equal("Cancelled", merged.Status.ToString());
        Assert.Empty(merged.Items);
        Assert.Equal(0, merged.SubTotal);
        var mergedTable = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{tables[1].Id}"));
        Assert.Equal("InUse", mergedTable.Status.ToString());
        Assert.Equal(target.Id, mergedTable.ActiveOrder?.Id);
        var attempts = await Task.WhenAll(
            waiter.PostAsJsonAsync("/api/don-hang", new { type = "DineIn", tableId = tables[3].Id }),
            waiter.PostAsJsonAsync("/api/don-hang", new { type = "DineIn", tableId = tables[3].Id }));
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, (await Data<PageResponse<OrderResponse>>(await waiter.GetAsync($"/api/don-hang?tableId={tables[3].Id}"))).TotalCount);
    }

    [SqlServerFact]
    public async Task TableUseCasesSeatTransferMergeCleaningAndManagerManagement()
    {
        await using var host = await ApiHost.CreateAsync();
        using var waiter = await SignIn(host, "waiter");
        using var manager = await SignIn(host, "manager");
        using var barista = await SignIn(host, "barista");
        Assert.Equal(HttpStatusCode.Forbidden, (await barista.GetAsync("/api/ban")).StatusCode);

        var tables = (await Data<PageResponse<TableResponse>>(await waiter.GetAsync("/api/ban"))).Items;
        var source = tables[0];
        var firstTarget = tables[1];
        var secondSource = tables[2];
        var seated = await Data<TableResponse>(await waiter.PostAsJsonAsync($"/api/ban/{source.Id}/seat",
            new { rowVersion = source.RowVersion }));
        Assert.Equal("InUse", seated.Status.ToString());
        Assert.Null(seated.ActiveOrder);
        Assert.Equal(HttpStatusCode.Conflict, (await waiter.PostAsJsonAsync($"/api/ban/{source.Id}/seat",
            new { rowVersion = source.RowVersion })).StatusCode);

        var firstOrderResponse = await waiter.PostAsJsonAsync("/api/don-hang", new
        {
            type = "DineIn", tableId = source.Id
        });
        Assert.Equal(HttpStatusCode.Created, firstOrderResponse.StatusCode);
        var firstOrder = await Data<OrderResponse>(firstOrderResponse);
        var linkedSource = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{source.Id}"));
        Assert.Equal(firstOrder.Id, linkedSource.ActiveOrder?.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await waiter.PostAsJsonAsync("/api/don-hang",
            new { type = "DineIn", tableId = source.Id })).StatusCode);

        var firstTargetCurrent = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{firstTarget.Id}"));
        firstOrder = await Data<OrderResponse>(await waiter.GetAsync($"/api/don-hang/{firstOrder.Id}"));
        firstOrder = await Data<OrderResponse>(await waiter.PostAsJsonAsync("/api/ban/transfer", new
        {
            sourceTableId = source.Id,
            targetTableId = firstTarget.Id,
            orderRowVersion = firstOrder.RowVersion,
            sourceTableRowVersion = linkedSource.RowVersion,
            targetTableRowVersion = firstTargetCurrent.RowVersion
        }));
        var sourceAfterTransfer = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{source.Id}"));
        var targetAfterTransfer = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{firstTarget.Id}"));
        Assert.Equal("NeedsCleaning", sourceAfterTransfer.Status.ToString());
        Assert.Null(sourceAfterTransfer.ActiveOrder);
        Assert.Equal(firstOrder.Id, targetAfterTransfer.ActiveOrder?.Id);
        Assert.Equal(HttpStatusCode.OK, (await waiter.PatchAsJsonAsync($"/api/ban/{source.Id}/status",
            new { status = "Available", rowVersion = sourceAfterTransfer.RowVersion })).StatusCode);

        var secondOrder = await Data<OrderResponse>(await waiter.PostAsJsonAsync("/api/don-hang",
            new { type = "DineIn", tableId = secondSource.Id }));
        var thirdSource = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{tables[3].Id}"));
        var tableSourceForMerge = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{firstTarget.Id}"));
        var tableTargetForMerge = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{secondSource.Id}"));
        var mergeWithoutOrderSelection = await waiter.PostAsJsonAsync("/api/ban/merge", new
        {
            targetTableId = secondSource.Id,
            targetTableRowVersion = tableTargetForMerge.RowVersion,
            targetActiveOrderRowVersion = secondOrder.RowVersion,
            sources = new[]
            {
                new { tableId = firstTarget.Id, tableRowVersion = tableSourceForMerge.RowVersion, activeOrderRowVersion = (string?)firstOrder.RowVersion },
                new { tableId = thirdSource.Id, tableRowVersion = thirdSource.RowVersion, activeOrderRowVersion = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, mergeWithoutOrderSelection.StatusCode);
        var duplicateSource = await waiter.PostAsJsonAsync("/api/ban/merge", new
        {
            targetTableId = secondSource.Id,
            targetTableRowVersion = tableTargetForMerge.RowVersion,
            targetActiveOrderRowVersion = secondOrder.RowVersion,
            targetOrderId = secondOrder.Id,
            targetOrderRowVersion = secondOrder.RowVersion,
            sources = new[]
            {
                new { tableId = firstTarget.Id, tableRowVersion = tableSourceForMerge.RowVersion, activeOrderRowVersion = (string?)firstOrder.RowVersion },
                new { tableId = firstTarget.Id, tableRowVersion = tableSourceForMerge.RowVersion, activeOrderRowVersion = (string?)firstOrder.RowVersion }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, duplicateSource.StatusCode);
        var merge = await Data<MergeTablesResponse>(await waiter.PostAsJsonAsync("/api/ban/merge", new
        {
            targetTableId = secondSource.Id,
            targetTableRowVersion = tableTargetForMerge.RowVersion,
            targetActiveOrderRowVersion = secondOrder.RowVersion,
            targetOrderId = secondOrder.Id,
            targetOrderRowVersion = secondOrder.RowVersion,
            sources = new[]
            {
                new
                {
                    tableId = firstTarget.Id,
                    tableRowVersion = tableSourceForMerge.RowVersion,
                    activeOrderRowVersion = (string?)firstOrder.RowVersion
                },
                new
                {
                    tableId = thirdSource.Id,
                    tableRowVersion = thirdSource.RowVersion,
                    activeOrderRowVersion = (string?)null
                }
            }
        }));
        Assert.Equal(secondSource.Id, merge.TargetTableId);
        Assert.Equal(new[] { firstTarget.Id, thirdSource.Id }, merge.SourceTableIds);
        Assert.Equal(secondOrder.Id, merge.ActiveOrder?.Id);
        var mergedSourceTable = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{firstTarget.Id}"));
        var mergedThirdTable = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{thirdSource.Id}"));
        var mergedTargetTable = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{secondSource.Id}"));
        Assert.Equal("InUse", mergedSourceTable.Status.ToString());
        Assert.Equal(secondOrder.Id, mergedSourceTable.ActiveOrder?.Id);
        Assert.Equal(secondOrder.Id, mergedThirdTable.ActiveOrder?.Id);
        Assert.Equal(secondOrder.Id, mergedTargetTable.ActiveOrder?.Id);
        Assert.Equal("Cancelled", (await Data<OrderResponse>(await waiter.GetAsync($"/api/don-hang/{firstOrder.Id}"))).Status.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await manager.DeleteAsync(
            $"/api/ban/{mergedSourceTable.Id}?rowVersion={Uri.EscapeDataString(mergedSourceTable.RowVersion)}")).StatusCode);

        var emptyMergeSource = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{tables[4].Id}"));
        var emptyMergeTarget = await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{tables[5].Id}"));
        var emptyMerge = await Data<MergeTablesResponse>(await waiter.PostAsJsonAsync("/api/ban/merge", new
        {
            targetTableId = emptyMergeTarget.Id,
            targetTableRowVersion = emptyMergeTarget.RowVersion,
            targetActiveOrderRowVersion = (string?)null,
            sources = new[] { new
            {
                tableId = emptyMergeSource.Id,
                tableRowVersion = emptyMergeSource.RowVersion,
                activeOrderRowVersion = (string?)null
            } }
        }));
        Assert.Null(emptyMerge.ActiveOrder);
        Assert.Equal("InUse", (await Data<TableResponse>(await waiter.GetAsync(
            $"/api/ban/{emptyMergeSource.Id}"))).Status.ToString());

        var newCode = $"U{Guid.NewGuid():N}"[..9];
        var created = await Data<TableResponse>(await manager.PostAsJsonAsync("/api/ban",
            new { code = newCode, area = "Khu thử nghiệm", note = "Bàn mới" }));
        Assert.Equal("Available", created.Status.ToString());
        var changed = await Data<TableResponse>(await manager.PutAsJsonAsync($"/api/ban/{created.Id}", new
        {
            code = created.Code, area = "Khu thử nghiệm 2", note = created.Note, rowVersion = created.RowVersion
        }));
        Assert.Equal("Khu thử nghiệm 2", changed.Area);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PutAsJsonAsync($"/api/ban/{created.Id}", new
        {
            code = created.Code, area = "Thông tin cũ", note = created.Note, rowVersion = created.RowVersion
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync(
            $"/api/ban/{changed.Id}?rowVersion={Uri.EscapeDataString(changed.RowVersion)}")).StatusCode);
        var disabled = await Data<TableResponse>(await manager.GetAsync($"/api/ban/{created.Id}"));
        Assert.Equal("Disabled", disabled.Status.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await manager.DeleteAsync(
            $"/api/ban/{disabled.Id}?rowVersion={Uri.EscapeDataString(disabled.RowVersion)}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await waiter.PostAsJsonAsync("/api/ban",
            new { code = "NOMGR", area = "Không có quyền" })).StatusCode);
    }

    [SqlServerFact]
    public async Task CatalogCrudValidationAndFailedOrderRollbackPreserveData()
    {
        await using var host = await ApiHost.CreateAsync();
        using var manager = await SignIn(host, "manager");
        using var waiter = await SignIn(host, "waiter");
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync("/api/mon",
            new { name = " ", basePrice = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await waiter.GetAsync("/api/ban?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await waiter.GetAsync($"/api/don-hang/{Guid.NewGuid()}")).StatusCode);
        var category = await Data<CategoryResponse>(await manager.PostAsJsonAsync("/api/danh-muc", new { name = "Test category" }));
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync("/api/danh-muc", new { name = "Test category" })).StatusCode);

        var existingMenu = (await Data<PageResponse<MenuItemResponse>>(await waiter.GetAsync("/api/mon"))).Items
            .First(x => x.Sizes.Count > 0);
        var size = await Data<SizeResponse>(await manager.PostAsJsonAsync("/api/sizes", new
        {
            menuItemId = existingMenu.Id, name = "XL", priceDelta = 15000
        }));
        var topping = await Data<ToppingResponse>(await manager.PostAsJsonAsync("/api/toppings", new
        {
            menuItemId = existingMenu.Id, name = "Extra shot", price = 10000
        }));
        var menu = await Data<MenuItemResponse>(await manager.PostAsJsonAsync("/api/mon", new
        {
            categoryId = category.Id, name = "Test drink",
            sizes = new[] { new { name = "Regular", price = 50000 }, new { name = "Large", price = 65000 } },
            toppings = new[] { new { name = "Extra shot", price = 10000 } }
        }));
        Assert.Equal(2, menu.Sizes.Count);
        Assert.Single(menu.Toppings);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync("/api/mon", new
        {
            categoryId = category.Id, name = "Test drink",
            sizes = new[] { new { name = "Regular", price = 50000 } }
        })).StatusCode);

        var categoryRename = await Data<CategoryResponse>(await manager.PutAsJsonAsync($"/api/danh-muc/{category.Id}",
            new { name = "Renamed test category" }));
        var renamedMenu = await Data<MenuItemResponse>(await manager.GetAsync($"/api/mon/{menu.Id}"));
        Assert.Equal(category.Id, renamedMenu.CategoryId);
        Assert.Equal(categoryRename.Name, renamedMenu.CategoryName);

        var updatedMenu = await Data<MenuItemResponse>(await manager.PutAsJsonAsync($"/api/mon/{menu.Id}", new
        {
            categoryId = menu.CategoryId,
            name = menu.Name,
            sizes = menu.Sizes.Select(x => new { sizeId = x.SizeId, name = x.Name, price = x.Price + 1000 }),
            toppings = menu.Toppings.Select(x => new { toppingId = x.Id, name = x.Name, price = x.Price + 1000 }),
            rowVersion = menu.RowVersion
        }));
        Assert.Equal(51000, updatedMenu.Sizes.Single(x => x.Name == "Regular").Price);
        Assert.Equal(11000, updatedMenu.Toppings.Single().Price);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PutAsJsonAsync($"/api/mon/{menu.Id}", new
        {
            categoryId = menu.CategoryId,
            name = menu.Name,
            sizes = menu.Sizes.Select(x => new { sizeId = x.SizeId, name = x.Name, price = x.Price }),
            toppings = menu.Toppings.Select(x => new { toppingId = x.Id, name = x.Name, price = x.Price }),
            rowVersion = menu.RowVersion
        })).StatusCode);

        var unavailable = await Data<MenuItemResponse>(await manager.PatchAsJsonAsync($"/api/mon/{menu.Id}/status",
            new { status = "OutOfStock", rowVersion = updatedMenu.RowVersion }));
        Assert.DoesNotContain((await Data<PageResponse<MenuItemResponse>>(await waiter.GetAsync("/api/mon"))).Items,
            x => x.Id == menu.Id);
        Assert.Contains((await Data<PageResponse<MenuItemResponse>>(
            await manager.GetAsync("/api/mon?orderableOnly=false"))).Items, x => x.Id == menu.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PatchAsJsonAsync($"/api/mon/{menu.Id}/status",
            new { status = "Available", rowVersion = updatedMenu.RowVersion })).StatusCode);
        var restored = await Data<MenuItemResponse>(await manager.PatchAsJsonAsync($"/api/mon/{menu.Id}/status",
            new { status = "Available", rowVersion = unavailable.RowVersion }));

        var table = await Data<TableResponse>(await manager.PostAsJsonAsync("/api/ban", new { code = "TEST", area = "Test area" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await waiter.PostAsJsonAsync("/api/don-hang", new
        {
            type = "DineIn", tableId = table.Id, items = new[] { new { menuItemId = restored.Id, quantity = 1 } }
        })).StatusCode);
        Assert.Equal("Available", (await Data<TableResponse>(await waiter.GetAsync($"/api/ban/{table.Id}"))).Status.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await waiter.PostAsJsonAsync("/api/don-hang", new
        {
            type = "TakeAway", items = new[] { new { menuItemId = existingMenu.Id, sizeId = size.Id, quantity = 1,
                toppings = new[] { new { toppingId = topping.Id, quantity = 1 }, new { toppingId = topping.Id, quantity = 1 } } } }
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PutAsJsonAsync($"/api/sizes/{size.Id}",
            new { menuItemId = existingMenu.Id, name = "XXL", priceDelta = 20000 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PutAsJsonAsync($"/api/toppings/{topping.Id}",
            new { menuItemId = existingMenu.Id, name = "Extra shot", price = 12000 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/api/sizes/{size.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/api/toppings/{topping.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.DeleteAsync($"/api/danh-muc/{category.Id}")).StatusCode);
        Assert.Contains((await Data<PageResponse<MenuItemResponse>>(await waiter.GetAsync("/api/mon"))).Items, x => x.Id == restored.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.DeleteAsync($"/api/mon/{menu.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync(
            $"/api/mon/{menu.Id}?rowVersion={Uri.EscapeDataString(restored.RowVersion)}")).StatusCode);
        Assert.Equal("Discontinued",
            (await Data<MenuItemResponse>(await manager.GetAsync($"/api/mon/{menu.Id}"))).Status.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/api/ban/{table.Id}?rowVersion={Uri.EscapeDataString(table.RowVersion ?? string.Empty)}")).StatusCode);
    }

    private static async Task<HttpClient> SignIn(ApiHost host, string username, string? password = null)
    {
        var client = host.CreateClient();
        password ??= char.ToUpperInvariant(username[0]) + username[1..] + "@123";
        var result = await Data<LoginResponse>(await client.PostAsJsonAsync("/api/auth/login", new { username, password }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Token);
        return client;
    }

    private static async Task<T> Data<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        var envelope = JsonSerializer.Deserialize<ApiResponse<T>>(body, Json);
        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        return envelope.Data;
    }

    private static async Task AssertError(HttpResponseMessage response)
    {
        var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(Json);
        Assert.NotNull(error);
        Assert.False(error.Success);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

}

internal static class HttpTestExtensions
{
    public static Task<HttpResponseMessage> DeleteWithJson<T>(this HttpClient client, string uri, T body) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, uri) { Content = JsonContent.Create(body) });
}

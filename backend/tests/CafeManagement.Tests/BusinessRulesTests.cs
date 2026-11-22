using CafeManagement.Domain.Common;
using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using CafeManagement.Domain.Rules;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CafeManagement.Tests;

public sealed class BusinessRulesTests
{
    [Fact]
    public void CashPaymentCalculatesNetTotalAndChange()
    {
        var amounts = PaymentRules.Calculate(100000m, 20000m, PaymentMethod.Cash, 100000m);

        Assert.Equal(80000m, amounts.TotalAmount);
        Assert.Equal(100000m, amounts.ReceivedAmount);
        Assert.Equal(20000m, amounts.ChangeAmount);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, 79999)]
    [InlineData(PaymentMethod.Card, 100000)]
    [InlineData(PaymentMethod.BankTransfer, 1)]
    public void PaymentRejectsInsufficientOrMismatchedAmount(PaymentMethod method, decimal received)
    {
        Assert.Throws<DomainException>(() =>
            PaymentRules.Calculate(100000m, 20000m, method, received));
    }

    [Fact]
    public void RevenueIncludesOnlyPaidInvoices()
    {
        var invoices = new[]
        {
            new HoaDon { TrangThai = InvoiceStatus.Paid, TongThanhToan = 100000m },
            new HoaDon { TrangThai = InvoiceStatus.Cancelled, TongThanhToan = 80000m }
        }.AsQueryable();

        var paid = RevenueRules.PaidInvoices(invoices);

        Assert.Single(paid);
        Assert.Equal(100000m, paid.Sum(x => x.TongThanhToan));
    }

    [Fact]
    public void VietnameseTextColumnsUseUnicodeStorage()
    {
        var options = new DbContextOptionsBuilder<CafeDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=metadata-only")
            .Options;

        using var db = new CafeDbContext(options);
        Assert.Equal("nvarchar(100)", db.Model.FindEntityType(typeof(Mon))!.FindProperty(nameof(Mon.TenMon))!.GetColumnType());
        Assert.Equal("nvarchar(max)", db.Model.FindEntityType(typeof(Mon))!.FindProperty(nameof(Mon.MoTa))!.GetColumnType());
        Assert.Equal("nvarchar(100)", db.Model.FindEntityType(typeof(DanhMuc))!.FindProperty(nameof(DanhMuc.TenDanhMuc))!.GetColumnType());
        Assert.Equal("varchar(10)", db.Model.FindEntityType(typeof(Mon))!.FindProperty(nameof(Mon.MaMon))!.GetColumnType());
    }
}

using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Domain.Rules;

public static class RevenueRules
{
    public static IQueryable<HoaDon> PaidInvoices(IQueryable<HoaDon> invoices) =>
        invoices.Where(x => x.TrangThai == InvoiceStatus.Paid);
}

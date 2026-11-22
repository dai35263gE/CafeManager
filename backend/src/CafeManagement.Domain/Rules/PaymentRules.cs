using CafeManagement.Domain.Common;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Domain.Rules;

public sealed record PaymentAmounts(decimal TotalAmount, decimal ReceivedAmount, decimal ChangeAmount);

public static class PaymentRules
{
    public static PaymentAmounts Calculate(decimal subTotal, decimal discount, PaymentMethod method, decimal received,
        decimal vat = 0, decimal serviceFee = 0)
    {
        if (!Enum.IsDefined(method) || subTotal < 0 || discount < 0 || discount > subTotal ||
            vat < 0 || serviceFee < 0 || received < 0 || decimal.Round(received, 2) != received)
            throw new DomainException("Invalid payment amounts or method.");
        var total = subTotal + vat + serviceFee - discount;
        if (received < total)
            throw new DomainException("The received amount is insufficient.");
        if (method != PaymentMethod.Cash && received != total)
            throw new DomainException("Noncash payments must match the exact total; change is only available for cash.");
        return new(total, received, received - total);
    }
}

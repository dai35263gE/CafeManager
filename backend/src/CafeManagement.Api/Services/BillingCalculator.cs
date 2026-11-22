using System.Globalization;
using CafeManagement.Api.Contracts;
using Microsoft.Extensions.Configuration;

namespace CafeManagement.Api.Services;

internal sealed record BillingCalculation(
    decimal VatRatePercent,
    decimal VatAmount,
    decimal ServiceFeeRatePercent,
    decimal ServiceFeeAmount,
    decimal TotalAmount,
    IReadOnlyList<string> Warnings);

internal static class BillingCalculator
{
    public static BillingCalculation Calculate(
        IConfiguration configuration, decimal subTotal, decimal discountAmount)
    {
        var (vatRate, vatWarning) = ReadRate(configuration, "VatRatePercent", "thuế VAT");
        var (serviceRate, serviceWarning) = ReadRate(configuration, "ServiceFeeRatePercent", "phí dịch vụ");
        var discountedAmount = subTotal - discountAmount;
        var vat = decimal.Round(discountedAmount * vatRate / 100m, 2, MidpointRounding.AwayFromZero);
        var serviceFee = decimal.Round(discountedAmount * serviceRate / 100m, 2, MidpointRounding.AwayFromZero);
        var warnings = new[] { vatWarning, serviceWarning }
            .Where(warning => warning is not null)
            .Select(warning => warning!)
            .ToArray();
        return new(vatRate, vat, serviceRate, serviceFee, discountedAmount + vat + serviceFee, warnings);
    }

    private static (decimal Rate, string? Warning) ReadRate(
        IConfiguration configuration, string key, string displayName)
    {
        var raw = configuration[$"Billing:{key}"];
        const NumberStyles style = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (decimal.TryParse(raw, style, CultureInfo.InvariantCulture, out var rate) &&
            rate is >= 0 and <= 100)
            return (rate, null);
        return (0, $"Cấu hình {displayName} không hợp lệ; đã áp dụng mức 0%.");
    }
}

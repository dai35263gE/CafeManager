using System.Globalization;
using System.Text;

namespace CafeManagement.Api.Utils;

internal static class CsvWriter
{
    public static byte[] Write(IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Join(",", headers.Select(Cell)));
        foreach (var row in rows)
            text.AppendLine(string.Join(",", row.Select(Cell)));
        // UTF-8 BOM allows Excel to recognize Vietnamese labels and names.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static string Cell(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        // Formula injection is possible even when a CSV cell is quoted.
        if (value is string && text.TrimStart().StartsWithAnyFormulaPrefix())
            text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private static bool StartsWithAnyFormulaPrefix(this string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n';
}

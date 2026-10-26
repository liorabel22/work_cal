using System.Globalization;
using MauiDbManager.Models;

namespace MauiDbManager.Parsing;

/// <summary>Parses values such as 1, "1, 4", "1-3", or "1, 4-6".</summary>
public static class LowIdSelectorParser
{
    private const int MaximumExpandedValues = 10_000;

    public static IdSelectionResult Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return IdSelectionResult.Failure("Enter at least one low ID.");

        var normalized = input.Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal);
        var tokens = normalized.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var values = new SortedSet<long>();

        foreach (var token in tokens)
        {
            var rangeParts = token.Split('-', StringSplitOptions.TrimEntries);
            if (rangeParts.Length == 1)
            {
                if (!TryParseNonNegative(rangeParts[0], out var value))
                    return IdSelectionResult.Failure($"'{token}' is not a valid non-negative low ID.");

                values.Add(value);
            }
            else if (rangeParts.Length == 2)
            {
                if (!TryParseNonNegative(rangeParts[0], out var start) ||
                    !TryParseNonNegative(rangeParts[1], out var end) ||
                    end < start)
                {
                    return IdSelectionResult.Failure($"'{token}' is not a valid ascending low-ID range.");
                }

                if ((decimal)end - start + 1 > MaximumExpandedValues)
                    return IdSelectionResult.Failure($"Range '{token}' contains too many values.");

                for (var value = start; value <= end; value++)
                {
                    values.Add(value);
                    if (values.Count > MaximumExpandedValues)
                        return IdSelectionResult.Failure($"Select at most {MaximumExpandedValues:N0} low IDs.");

                    if (value == long.MaxValue)
                        break;
                }
            }
            else
            {
                return IdSelectionResult.Failure($"'{token}' is not a valid low ID or range.");
            }
        }

        return values.Count == 0
            ? IdSelectionResult.Failure("Enter at least one low ID.")
            : IdSelectionResult.Success(values.ToArray());
    }

    private static bool TryParseNonNegative(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;
}

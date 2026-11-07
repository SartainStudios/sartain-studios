using System.Globalization;
using System.Text.RegularExpressions;

namespace SartainStudios.Client.Service;

public static partial class EarningsScreenshotParser
{
    public sealed record Reading(decimal Amount, TimeSpan Start, TimeSpan? End);

    public static IReadOnlyList<Reading> ReadUber(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var results = new List<Reading>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("tip", StringComparison.OrdinalIgnoreCase))
                continue;
            var amount = UberAmount().Match(lines[i]);
            if (!amount.Success || !decimal.TryParse(amount.Groups[1].Value, NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var value))
                continue;
            for (var j = i; j <= Math.Min(i + 2, lines.Length - 1); j++)
            {
                if (j > i && UberAmount().IsMatch(lines[j]))
                    break;
                var match = TimePattern().Match(lines[j]);
                if (!match.Success || !TryTime(match.Value, out var time))
                    continue;
                results.Add(new Reading(value, time, null));
                i = j;
                break;
            }
        }
        return results;
    }

    public static IReadOnlyList<Reading> ReadDoorDash(string text)
    {
        var startMatch = StartPattern().Match(text);
        var endMatch = EndPattern().Match(text);
        if (!startMatch.Success || !endMatch.Success ||
            !TryTime(startMatch.Groups[1].Value, out var start) ||
            !TryTime(endMatch.Groups[1].Value, out var end))
            return [];
        var total = DollarAmount().Match(text[..startMatch.Index]);
        if (!total.Success || !decimal.TryParse(total.Groups[1].Value, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var amount))
            return [];
        return [new Reading(amount, start, end)];
    }

    private static bool TryTime(string value, out TimeSpan result)
    {
        if (DateTime.TryParseExact(value.Trim(), ["h:mm tt", "hh:mm tt", "h:mmtt", "hh:mmtt"],
                CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date))
        {
            result = date.TimeOfDay;
            return true;
        }
        result = default;
        return false;
    }

    [GeneratedRegex(@"^\s*(?:\S{1,3}\s+)?\$?\s*(\d{1,6}\.\d{2})(?=\s|$)")]
    private static partial Regex UberAmount();

    [GeneratedRegex(@"\$\s*(\d{1,6}\.\d{2})")]
    private static partial Regex DollarAmount();

    [GeneratedRegex(@"\b\d{1,2}:\d{2}\s*[AP]M\b", RegexOptions.IgnoreCase)]
    private static partial Regex TimePattern();

    [GeneratedRegex(@"Start\s+Time\s+(\d{1,2}:\d{2}\s*[AP]M)", RegexOptions.IgnoreCase)]
    private static partial Regex StartPattern();

    [GeneratedRegex(@"End\s+Time\s+(\d{1,2}:\d{2}\s*[AP]M)", RegexOptions.IgnoreCase)]
    private static partial Regex EndPattern();
}
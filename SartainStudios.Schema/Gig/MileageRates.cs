namespace SartainStudios.Schema.Gig;

public static class MileageRates
{
    private static readonly IReadOnlyList<(DateOnly EffectiveFrom, decimal Rate)> Rates =
    [
        (new DateOnly(2023, 1, 1), 0.655m),
        (new DateOnly(2024, 1, 1), 0.67m),
        (new DateOnly(2025, 1, 1), 0.70m),
        (new DateOnly(2026, 1, 1), 0.725m),
        (new DateOnly(2026, 7, 1), 0.76m)
    ];

    public static decimal ForDate(DateOnly date)
    {
        var rate = Rates[0].Rate;
        foreach (var entry in Rates)
        {
            if (entry.EffectiveFrom > date) break;
            rate = entry.Rate;
        }

        return rate;
    }

    public static decimal ForYear(int year)
    {
        return ForDate(new DateOnly(Math.Clamp(year, 1, 9999), 12, 31));
    }
}
namespace SartainStudios.Client.Component;

public static class GigFormat
{
    public static string Money(decimal value)
    {
        return value < 0 ? $"-${Math.Abs(value):N2}" : $"${value:N2}";
    }

    public static string Rate(decimal? value)
    {
        return value is { } rate ? $"{Money(rate)}/h" : "-";
    }

    public static string Hours(int minutes)
    {
        var hours = Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);
        return $"{hours:F2}h";
    }

    public static string Clock(TimeSpan elapsed)
    {
        var total = Math.Max(0, (int)Math.Floor(elapsed.TotalSeconds));
        return $"{total / 3600:00}:{total % 3600 / 60:00}:{total % 60:00}";
    }
}
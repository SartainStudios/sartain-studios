using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Service;

public static class EarningsScreenshotSource
{
    public const string Uber = "Uber";
    public const string DoorDash = "DoorDash";

    public static Source? Find(string platform, IEnumerable<Source> sources)
    {
        var name = platform switch
        {
            Uber => GigDefaults.UberEatsSourceName,
            DoorDash => GigDefaults.DoorDashSourceName,
            _ => null
        };
        if (name is null)
            return null;

        var matches = sources.Where(source => source.IsDefault && !source.IsHidden &&
            string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }
}
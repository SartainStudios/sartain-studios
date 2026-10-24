namespace SartainStudios.Schema.Gig;

public static class GigDefaults
{
    public const string FallbackColor = "#757575";

    public static readonly IReadOnlyList<(string Name, string Color)> Sources =
    [
        ("Uber Eats", "#06C167"),
        ("DoorDash", "#EB1700"),
        ("Grubhub", "#F63440"),
        ("Instacart", "#43B02A"),
        ("Uber", "#276EF1"),
        ("Lyft", "#FF00BF"),
        ("Spark Driver", "#0071CE"),
        ("Shipt", "#E4002B"),
        ("Amazon Flex", "#FF9900"),
        ("Roadie", "#00A3E0"),
        ("Gopuff", "#1F57D6"),
        ("Cash", "#2E7D32"),
        ("Other", "#757575")
    ];

    public static readonly IReadOnlyList<(string Name, string Color)> Categories =
    [
        ("Fuel", "#F57C00"),
        ("Car Maintenance", "#5D4037"),
        ("Parking", "#455A64"),
        ("Tolls", "#7B1FA2"),
        ("Phone", "#1976D2"),
        ("Supplies", "#00796B"),
        ("Other", "#757575")
    ];
}
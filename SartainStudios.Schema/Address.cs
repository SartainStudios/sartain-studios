namespace SartainStudios.Schema;

public sealed class Address
{
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string StateOrProvince { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    public bool HasValue =>
        !string.IsNullOrWhiteSpace(Line1)
        || !string.IsNullOrWhiteSpace(Line2)
        || !string.IsNullOrWhiteSpace(City)
        || !string.IsNullOrWhiteSpace(StateOrProvince)
        || !string.IsNullOrWhiteSpace(PostalCode)
        || !string.IsNullOrWhiteSpace(Country);

    public IReadOnlyList<string> ToLines()
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(Line1)) lines.Add(Line1.Trim());
        if (!string.IsNullOrWhiteSpace(Line2)) lines.Add(Line2.Trim());
        var cityLine = new List<string>();
        if (!string.IsNullOrWhiteSpace(City)) cityLine.Add(City.Trim());
        if (!string.IsNullOrWhiteSpace(StateOrProvince)) cityLine.Add(StateOrProvince.Trim());
        if (!string.IsNullOrWhiteSpace(PostalCode)) cityLine.Add(PostalCode.Trim());
        if (cityLine.Count > 0) lines.Add(string.Join(", ", cityLine));
        if (!string.IsNullOrWhiteSpace(Country)) lines.Add(Country.Trim());
        return lines;
    }

    public override string ToString()
    {
        return string.Join(", ", ToLines());
    }

    public Address Trimmed()
    {
        return new Address
        {
            Line1 = Line1.Trim(),
            Line2 = Line2.Trim(),
            City = City.Trim(),
            StateOrProvince = StateOrProvince.Trim(),
            PostalCode = PostalCode.Trim(),
            Country = Country.Trim()
        };
    }
}
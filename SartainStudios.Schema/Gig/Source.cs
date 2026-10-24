namespace SartainStudios.Schema.Gig;

public sealed record Source(string Id, string Name, string Color, bool IsDefault, bool IsHidden);

public sealed record Category(string Id, string Name, string Color, bool IsDefault, bool IsHidden);

public sealed record OptionRequest(string Name, string? Color);

public sealed record HiddenRequest(bool IsHidden);
namespace SartainStudios.Schema.Invoice;

public static class Lifecycle
{
    public const string AnyStatus = "All";

    private static readonly IReadOnlyDictionary<Status, IReadOnlyList<Status>> Transitions =
        new Dictionary<Status, IReadOnlyList<Status>>
        {
            [Status.Draft] = [Status.Sent, Status.Paid, Status.Overdue],
            [Status.Sent] = [Status.Paid, Status.Overdue],
            [Status.Overdue] = [Status.Paid],
            [Status.Paid] = []
        };

    public static IReadOnlyList<string> Names { get; } = Enum.GetNames<Status>();
    public static string Options { get; } = string.Join(", ", Names);

    public static bool TryNormalize(string? status, out Status normalized)
    {
        normalized = default;
        if (string.IsNullOrWhiteSpace(status))
            return false;
        var name = Names.FirstOrDefault(candidate =>
            string.Equals(candidate, status.Trim(), StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return false;
        normalized = Enum.Parse<Status>(name);
        return true;
    }

    public static bool Is(string? status, Status expected)
    {
        return TryNormalize(status, out var normalized) && normalized == expected;
    }

    public static bool IsDraft(string? status)
    {
        return Is(status, Status.Draft);
    }

    public static IReadOnlyList<string> AllowedTransitionsFrom(string? currentStatus)
    {
        return TryNormalize(currentStatus, out var current)
            ? Transitions[current].Select(status => status.ToString()).ToList()
            : [];
    }

    public static bool CanTransition(string? currentStatus, string? nextStatus)
    {
        if (!TryNormalize(currentStatus, out var current) || !TryNormalize(nextStatus, out var next))
            return false;
        return current == next || Transitions[current].Contains(next);
    }
}
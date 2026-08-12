namespace SartainStudios.Api.Service.Validation;

public static class EnumName
{
    public static bool TryNormalize<TEnum>(string? value, out string normalized) where TEnum : struct, Enum
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        normalized = Enum.GetNames<TEnum>()
                         .FirstOrDefault(candidate =>
                             string.Equals(candidate, value.Trim(), StringComparison.OrdinalIgnoreCase))
                     ?? string.Empty;
        return normalized.Length > 0;
    }

    public static string Options<TEnum>() where TEnum : struct, Enum
    {
        return string.Join(", ", Enum.GetNames<TEnum>());
    }
}
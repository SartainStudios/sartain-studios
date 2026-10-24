namespace SartainStudios.Schema.Gig;

public enum VehicleType
{
    Car,
    Motorcycle,
    Bicycle,
    EBike,
    Other
}

public static class VehicleTypes
{
    public static bool IsDeductible(string? vehicleType)
    {
        return string.Equals(vehicleType, nameof(VehicleType.Car), StringComparison.OrdinalIgnoreCase)
               || string.Equals(vehicleType, nameof(VehicleType.Motorcycle), StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryNormalize(string? vehicleType, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(vehicleType))
        {
            normalized = nameof(VehicleType.Car);
            return true;
        }

        if (Enum.TryParse<VehicleType>(vehicleType.Trim(), true, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            normalized = parsed.ToString();
            return true;
        }

        normalized = string.Empty;
        return false;
    }
}
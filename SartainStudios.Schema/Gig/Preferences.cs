namespace SartainStudios.Schema.Gig;

public sealed record Preferences(
    bool MileageDeductionEnabled,
    decimal? MileageRateOverride,
    int RateYear,
    decimal EffectiveRate,
    decimal DefaultRate);

public sealed record UpdatePreferencesRequest(bool MileageDeductionEnabled, decimal? MileageRateOverride);
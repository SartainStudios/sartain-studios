using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Gig;

public sealed class GigSettingsService(
    Database database,
    CurrentTenant currentTenant,
    TimeProvider timeProvider)
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<Result<Preferences>> GetAsync(
        string? userTimeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        var settings = await FindAsync(userId, cancellationToken);
        return ToPreferences(settings, userTimeZoneId);
    }

    public async Task<Result<Preferences>> UpdateAsync(
        UpdatePreferencesRequest request,
        string? userTimeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        if (request.MileageRateOverride is { } rate &&
            (rate <= 0 || rate > GigErrors.MaximumMileageRate))
            return GigErrors.MileageRateInvalid;
        var settings = await SaveAsync(userId, existing =>
        {
            existing.MileageDeductionEnabled = request.MileageDeductionEnabled;
            existing.MileageRateOverride = request.MileageRateOverride is { } value
                ? Math.Round(value, 4, MidpointRounding.AwayFromZero)
                : null;
        }, cancellationToken);
        return ToPreferences(settings, userTimeZoneId);
    }

    public async Task<GigRateContext> GetRateContextAsync(ObjectId userId,
        CancellationToken cancellationToken = default)
    {
        var settings = await FindAsync(userId, cancellationToken);
        return new GigRateContext(settings.MileageDeductionEnabled, settings.MileageRateOverride);
    }

    public async Task<GigSettings> FindAsync(ObjectId userId, CancellationToken cancellationToken = default)
    {
        var existing = await database.GigSettings
            .Find(settings => settings.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
        return existing ?? new GigSettings { UserId = userId };
    }

    public async Task<GigSettings> SaveAsync(
        ObjectId userId,
        Action<GigSettings> mutate,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var existing = await database.GigSettings
                .Find(settings => settings.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                mutate(existing);
                existing.UpdatedAt = Now;
                await database.GigSettings.ReplaceOneAsync(
                    settings => settings.Id == existing.Id, existing, cancellationToken: cancellationToken);
                return existing;
            }

            var created = new GigSettings { UserId = userId };
            mutate(created);
            created.UpdatedAt = Now;
            try
            {
                await database.GigSettings.InsertOneAsync(created, cancellationToken: cancellationToken);
                return created;
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category ==
                                                        ServerErrorCategory.DuplicateKey)
            {
                // Another request created the settings first; retry against the stored document.
            }
        }

        throw new InvalidOperationException("Gig settings could not be saved.");
    }

    private Preferences ToPreferences(GigSettings settings, string? userTimeZoneId)
    {
        var timeZone = GigCalculator.ResolveTimeZone(userTimeZoneId);
        var today = GigCalculator.LocalDate(Now, timeZone);
        var defaultRate = MileageRates.ForDate(today);
        return new Preferences(
            settings.MileageDeductionEnabled,
            settings.MileageRateOverride,
            today.Year,
            settings.MileageRateOverride ?? defaultRate,
            defaultRate);
    }
}
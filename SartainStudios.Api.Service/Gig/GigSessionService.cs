using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Gig;

public sealed class GigSessionService(
    Database database,
    CurrentTenant currentTenant,
    GigSettingsService settingsService,
    TimeProvider timeProvider)
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<Result<GigSessionState>> GetCurrentAsync(
        string? userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        var open = await FindOpenAsync(userId, cancellationToken);
        var now = Now;
        if (open is null)
            return new GigSessionState(null, now);
        var rate = await settingsService.GetRateContextAsync(userId, cancellationToken);
        var timeZone = GigCalculator.ResolveTimeZone(userTimeZoneId);
        return new GigSessionState(GigCalculator.ToSession(open, rate, timeZone, now), now);
    }

    public async Task<Result<IReadOnlyList<GigSessionDto>>> ListAsync(
        int take,
        string? userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        if (take is < GigErrors.MinimumTake or > GigErrors.MaximumTake)
            return GigErrors.TakeOutOfRange;
        var sessions = await database.GigSessions
            .Find(session => session.UserId == userId)
            .SortByDescending(session => session.StartTime)
            .Limit(take)
            .ToListAsync(cancellationToken);
        var rate = await settingsService.GetRateContextAsync(userId, cancellationToken);
        var timeZone = GigCalculator.ResolveTimeZone(userTimeZoneId);
        var now = Now;
        IReadOnlyList<GigSessionDto> result = sessions
            .Select(session => GigCalculator.ToSession(session, rate, timeZone, now))
            .ToList();
        return Result.Success(result);
    }

    public async Task<Result<GigSessionDto>> GetAsync(
        string id,
        string? userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
            return loaded.Error;
        return await ToSessionAsync(loaded.Value, userTimeZoneId, cancellationToken);
    }

    public async Task<Result<GigSessionState>> StartAsync(
        StartGigSessionRequest request,
        string? userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        var now = Now;
        var startTime = request.StartTime ?? now;
        var validation = GigSessionValidation.Validate(
            startTime, null, request.StartOdometer, null, request.VehicleType, null, null, null, now);
        if (validation.IsFailure)
            return validation.Error;
        if (await FindOpenAsync(userId, cancellationToken) is not null)
            return GigErrors.SessionAlreadyOpen;
        var validated = validation.Value;
        var session = new GigSession
        {
            UserId = userId,
            StartTime = validated.StartTime,
            StartOdometer = validated.StartOdometer,
            VehicleType = validated.VehicleType,
            CreatedAt = now,
            UpdatedAt = now
        };
        try
        {
            await database.GigSessions.InsertOneAsync(session, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return GigErrors.SessionAlreadyOpen;
        }

        var rate = await settingsService.GetRateContextAsync(userId, cancellationToken);
        var timeZone = GigCalculator.ResolveTimeZone(userTimeZoneId);
        return new GigSessionState(GigCalculator.ToSession(session, rate, timeZone, now), now);
    }

    public async Task<Result<GigSessionDto>> StopAsync(
        StopGigSessionRequest request,
        string? userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        var session = await FindOpenAsync(userId, cancellationToken);
        if (session is null)
            return GigErrors.NoOpenSession;
        var now = Now;
        var validation = GigSessionValidation.Validate(
            request.StartTime ?? DateTime.SpecifyKind(session.StartTime, DateTimeKind.Utc),
            request.EndTime ?? now,
            request.StartOdometer ?? session.StartOdometer,
            request.EndOdometer,
            string.IsNullOrWhiteSpace(request.VehicleType) ? session.VehicleType : request.VehicleType,
            request.Notes,
            request.Earnings,
            request.Expenses,
            now);
        if (validation.IsFailure)
            return validation.Error;
        var references = await ValidateReferencesAsync(
            userId, validation.Value.Earnings, validation.Value.Expenses, cancellationToken);
        if (references.IsFailure)
            return references.Error;
        Apply(session, validation.Value, now);
        return await SaveAsync(session, userTimeZoneId, cancellationToken);
    }

    public async Task<Result<GigSessionDto>> CreateAsync(
        GigSessionRequest request,
        string? userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        if (request.EndTime is null)
            return ValidationError.FromErrors((GigErrors.EndTimeField, "End time is required."));
        var now = Now;
        var validation = Validate(request, now);
        if (validation.IsFailure)
            return validation.Error;
        var references = await ValidateReferencesAsync(
            userId, validation.Value.Earnings, validation.Value.Expenses, cancellationToken);
        if (references.IsFailure)
            return references.Error;
        var session = new GigSession { UserId = userId, CreatedAt = now };
        Apply(session, validation.Value, now);
        await database.GigSessions.InsertOneAsync(session, cancellationToken: cancellationToken);
        return await ToSessionAsync(session, userTimeZoneId, cancellationToken);
    }

    public async Task<Result<GigSessionDto>> UpdateAsync(
        string id,
        GigSessionRequest request,
        string? userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
            return loaded.Error;
        var session = loaded.Value;
        if (!session.IsOpen && request.EndTime is null)
            return ValidationError.FromErrors((GigErrors.EndTimeField, "End time is required."));
        var now = Now;
        var validation = Validate(request, now);
        if (validation.IsFailure)
            return validation.Error;
        var references = await ValidateReferencesAsync(
            session.UserId, validation.Value.Earnings, validation.Value.Expenses, cancellationToken);
        if (references.IsFailure)
            return references.Error;
        Apply(session, validation.Value, now);
        return await SaveAsync(session, userTimeZoneId, cancellationToken);
    }

    public async Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var sessionId))
            return GigErrors.InvalidId;
        var result = await database.GigSessions.DeleteOneAsync(
            session => session.Id == sessionId && session.UserId == userId, cancellationToken);
        return result.DeletedCount == 0 ? GigErrors.SessionNotFound : Result.Success();
    }

    private static Result<GigSessionValidation.ValidatedGigSession> Validate(GigSessionRequest request, DateTime now)
    {
        return GigSessionValidation.Validate(
            request.StartTime,
            request.EndTime,
            request.StartOdometer,
            request.EndOdometer,
            request.VehicleType,
            request.Notes,
            request.Earnings,
            request.Expenses,
            now);
    }

    private static void Apply(GigSession session, GigSessionValidation.ValidatedGigSession validated, DateTime now)
    {
        session.StartTime = validated.StartTime;
        session.EndTime = validated.EndTime;
        session.StartOdometer = validated.StartOdometer;
        session.EndOdometer = validated.EndOdometer;
        session.VehicleType = validated.VehicleType;
        session.Notes = validated.Notes;
        session.Earnings = validated.Earnings;
        session.Expenses = validated.Expenses;
        session.UpdatedAt = now;
    }

    private async Task<Result<GigSessionDto>> SaveAsync(
        GigSession session,
        string? userTimeZoneId,
        CancellationToken cancellationToken)
    {
        var result = await database.GigSessions.ReplaceOneAsync(
            existing => existing.Id == session.Id && existing.UserId == session.UserId,
            session,
            cancellationToken: cancellationToken);
        if (result.MatchedCount == 0)
            return GigErrors.SessionConflict;
        return await ToSessionAsync(session, userTimeZoneId, cancellationToken);
    }

    private async Task<GigSessionDto> ToSessionAsync(GigSession session, string? userTimeZoneId,
        CancellationToken cancellationToken)
    {
        var rate = await settingsService.GetRateContextAsync(session.UserId, cancellationToken);
        return GigCalculator.ToSession(session, rate, GigCalculator.ResolveTimeZone(userTimeZoneId), Now);
    }

    private async Task<Result<GigSession>> LoadAsync(string id, CancellationToken cancellationToken)
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var sessionId))
            return GigErrors.InvalidId;
        var session = await database.GigSessions
            .Find(candidate => candidate.Id == sessionId && candidate.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
        return session is null ? GigErrors.SessionNotFound : session;
    }

    private Task<GigSession?> FindOpenAsync(ObjectId userId, CancellationToken cancellationToken)
    {
        return database.GigSessions
            .Find(session => session.UserId == userId && session.IsOpen)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    private async Task<Result> ValidateReferencesAsync(
        ObjectId userId,
        IReadOnlyList<GigEarningEntry> earnings,
        IReadOnlyList<GigExpenseEntry> expenses,
        CancellationToken cancellationToken)
    {
        var errors = new List<(string Field, string Message)>();
        if (earnings.Count > 0)
        {
            var sources = await database.GigSources
                .Find(source => source.IsDefault || source.UserId == userId)
                .ToListAsync(cancellationToken);
            var available = sources.Select(source => source.Id).ToHashSet();
            for (var index = 0; index < earnings.Count; index++)
                if (!available.Contains(earnings[index].SourceId))
                    errors.Add(($"{GigErrors.EarningsField}[{index}]", "Choose a valid source."));
        }

        if (expenses.Count > 0)
        {
            var categories = await database.GigExpenseCategories
                .Find(category => category.IsDefault || category.UserId == userId)
                .ToListAsync(cancellationToken);
            var available = categories.Select(category => category.Id).ToHashSet();
            for (var index = 0; index < expenses.Count; index++)
                if (!available.Contains(expenses[index].CategoryId))
                    errors.Add(($"{GigErrors.ExpensesField}[{index}]", "Choose a valid category."));
        }

        return errors.Count > 0 ? ValidationError.FromErrors([.. errors]) : Result.Success();
    }
}
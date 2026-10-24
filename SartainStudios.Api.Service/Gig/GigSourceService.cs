using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Gig;

public sealed partial class GigSourceService(
    Database database,
    CurrentTenant currentTenant,
    GigSettingsService settingsService,
    TimeProvider timeProvider)
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private OptionKind<GigSource> SourceKind => new(
        database.GigSources,
        settings => settings.HiddenSourceIds,
        (settings, ids) => settings.HiddenSourceIds = ids,
        (userId, id) => database.GigSessions
            .Find(session => session.UserId == userId && session.Earnings.Any(entry => entry.SourceId == id))
            .AnyAsync());

    private OptionKind<GigExpenseCategory> CategoryKind => new(
        database.GigExpenseCategories,
        settings => settings.HiddenCategoryIds,
        (settings, ids) => settings.HiddenCategoryIds = ids,
        (userId, id) => database.GigSessions
            .Find(session => session.UserId == userId && session.Expenses.Any(entry => entry.CategoryId == id))
            .AnyAsync());

    public async Task<Result<IReadOnlyList<Source>>> ListSourcesAsync(CancellationToken cancellationToken = default)
    {
        var items = await ListAsync(SourceKind, cancellationToken);
        return items.IsFailure
            ? items.Error
            : Result.Success<IReadOnlyList<Source>>(items.Value
                .Select(item => new Source(item.Option.Id.ToString(), item.Option.Name, item.Option.Color,
                    item.Option.IsDefault, item.IsHidden))
                .ToList());
    }

    public async Task<Result<Source>> CreateSourceAsync(
        OptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await CreateAsync(SourceKind, request, cancellationToken);
        return created.IsFailure ? created.Error : ToSource(created.Value, false);
    }

    public async Task<Result<Source>> UpdateSourceAsync(
        string id,
        OptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await UpdateAsync(SourceKind, id, request, cancellationToken);
        return updated.IsFailure ? updated.Error : ToSource(updated.Value.Option, updated.Value.IsHidden);
    }

    public Task<Result> DeleteSourceAsync(string id, CancellationToken cancellationToken = default)
    {
        return DeleteAsync(SourceKind, id, cancellationToken);
    }

    public async Task<Result<Source>> SetSourceHiddenAsync(
        string id,
        HiddenRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await SetHiddenAsync(SourceKind, id, request.IsHidden, cancellationToken);
        return updated.IsFailure ? updated.Error : ToSource(updated.Value, request.IsHidden);
    }

    public async Task<Result<IReadOnlyList<Category>>> ListCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await ListAsync(CategoryKind, cancellationToken);
        return items.IsFailure
            ? items.Error
            : Result.Success<IReadOnlyList<Category>>(items.Value
                .Select(item => new Category(item.Option.Id.ToString(), item.Option.Name, item.Option.Color,
                    item.Option.IsDefault, item.IsHidden))
                .ToList());
    }

    public async Task<Result<Category>> CreateCategoryAsync(
        OptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await CreateAsync(CategoryKind, request, cancellationToken);
        return created.IsFailure ? created.Error : ToCategory(created.Value, false);
    }

    public async Task<Result<Category>> UpdateCategoryAsync(
        string id,
        OptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await UpdateAsync(CategoryKind, id, request, cancellationToken);
        return updated.IsFailure ? updated.Error : ToCategory(updated.Value.Option, updated.Value.IsHidden);
    }

    public Task<Result> DeleteCategoryAsync(string id, CancellationToken cancellationToken = default)
    {
        return DeleteAsync(CategoryKind, id, cancellationToken);
    }

    public async Task<Result<Category>> SetCategoryHiddenAsync(
        string id,
        HiddenRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await SetHiddenAsync(CategoryKind, id, request.IsHidden, cancellationToken);
        return updated.IsFailure ? updated.Error : ToCategory(updated.Value, request.IsHidden);
    }

    private async Task<Result<IReadOnlyList<ListedOption<T>>>> ListAsync<T>(
        OptionKind<T> kind,
        CancellationToken cancellationToken) where T : GigOption, new()
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        var options = await kind.Collection
            .Find(option => option.IsDefault || option.UserId == userId)
            .ToListAsync(cancellationToken);
        var settings = await settingsService.FindAsync(userId, cancellationToken);
        var hidden = kind.Hidden(settings).ToHashSet();
        IReadOnlyList<ListedOption<T>> listed = options
            .OrderByDescending(option => option.IsDefault)
            .ThenBy(option => option.IsDefault ? string.Empty : option.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Id)
            .Select(option => new ListedOption<T>(option, hidden.Contains(option.Id)))
            .ToList();
        return Result.Success(listed);
    }

    private async Task<Result<T>> CreateAsync<T>(
        OptionKind<T> kind,
        OptionRequest request,
        CancellationToken cancellationToken) where T : GigOption, new()
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        var validation = ValidateRequest(request);
        if (validation.IsFailure)
            return validation.Error;
        var (name, color) = (validation.Value.Name, validation.Value.Color);
        if (await IsNameTakenAsync(kind, userId, name, null, cancellationToken))
            return GigErrors.OptionNameTaken;
        var now = Now;
        var option = new T
        {
            UserId = userId,
            Name = name,
            Color = color,
            IsDefault = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        try
        {
            await kind.Collection.InsertOneAsync(option, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return GigErrors.OptionNameTaken;
        }

        return option;
    }

    private async Task<Result<ListedOption<T>>> UpdateAsync<T>(
        OptionKind<T> kind,
        string id,
        OptionRequest request,
        CancellationToken cancellationToken) where T : GigOption, new()
    {
        var loaded = await LoadOwnedAsync(kind, id, cancellationToken);
        if (loaded.IsFailure)
            return loaded.Error;
        var option = loaded.Value;
        var validation = ValidateRequest(request);
        if (validation.IsFailure)
            return validation.Error;
        var (name, color) = (validation.Value.Name, validation.Value.Color);
        if (await IsNameTakenAsync(kind, option.UserId!.Value, name, option.Id, cancellationToken))
            return GigErrors.OptionNameTaken;
        option.Name = name;
        option.Color = color;
        option.UpdatedAt = Now;
        try
        {
            await kind.Collection.ReplaceOneAsync(
                existing => existing.Id == option.Id, option, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return GigErrors.OptionNameTaken;
        }

        var settings = await settingsService.FindAsync(option.UserId!.Value, cancellationToken);
        return new ListedOption<T>(option, kind.Hidden(settings).Contains(option.Id));
    }

    private async Task<Result> DeleteAsync<T>(
        OptionKind<T> kind,
        string id,
        CancellationToken cancellationToken) where T : GigOption, new()
    {
        var loaded = await LoadOwnedAsync(kind, id, cancellationToken);
        if (loaded.IsFailure)
            return loaded.Error;
        var option = loaded.Value;
        var userId = option.UserId!.Value;
        if (await kind.IsInUseAsync(userId, option.Id))
            return GigErrors.OptionInUse;
        await kind.Collection.DeleteOneAsync(existing => existing.Id == option.Id, cancellationToken);
        var settings = await settingsService.FindAsync(userId, cancellationToken);
        if (kind.Hidden(settings).Contains(option.Id))
            await settingsService.SaveAsync(userId,
                existing => kind.SetHidden(existing,
                    kind.Hidden(existing).Where(hidden => hidden != option.Id).ToArray()),
                cancellationToken);
        return Result.Success();
    }

    private async Task<Result<T>> SetHiddenAsync<T>(
        OptionKind<T> kind,
        string id,
        bool isHidden,
        CancellationToken cancellationToken) where T : GigOption, new()
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var optionId))
            return GigErrors.InvalidId;
        var option = await kind.Collection
            .Find(candidate => candidate.Id == optionId && (candidate.IsDefault || candidate.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);
        if (option is null)
            return GigErrors.OptionNotFound;
        await settingsService.SaveAsync(userId, settings =>
        {
            var hidden = kind.Hidden(settings).Where(existing => existing != optionId).ToList();
            if (isHidden) hidden.Add(optionId);
            kind.SetHidden(settings, [.. hidden]);
        }, cancellationToken);
        return option;
    }

    private async Task<Result<T>> LoadOwnedAsync<T>(
        OptionKind<T> kind,
        string id,
        CancellationToken cancellationToken) where T : GigOption, new()
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var optionId))
            return GigErrors.InvalidId;
        var option = await kind.Collection
            .Find(candidate => candidate.Id == optionId && (candidate.IsDefault || candidate.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);
        if (option is null)
            return GigErrors.OptionNotFound;
        return option.IsDefault ? GigErrors.OptionIsDefault : option;
    }

    private static async Task<bool> IsNameTakenAsync<T>(
        OptionKind<T> kind,
        ObjectId userId,
        string name,
        ObjectId? excludingId,
        CancellationToken cancellationToken) where T : GigOption, new()
    {
        var visible = await kind.Collection
            .Find(option => option.IsDefault || option.UserId == userId)
            .ToListAsync(cancellationToken);
        return visible.Any(option => option.Id != excludingId
                                     && string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static Result<OptionValues> ValidateRequest(OptionRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            return GigErrors.NameRequired;
        if (name.Length > GigErrors.MaximumNameLength)
            return GigErrors.NameTooLong;
        var color = request.Color?.Trim();
        if (string.IsNullOrEmpty(color))
            return new OptionValues(name, GigDefaults.FallbackColor);
        if (!HexColor().IsMatch(color))
            return GigErrors.ColorInvalid;
        return new OptionValues(name, color.ToUpperInvariant());
    }

    private static Source ToSource(GigOption option, bool isHidden)
    {
        return new Source(option.Id.ToString(), option.Name, option.Color, option.IsDefault, isHidden);
    }

    private static Category ToCategory(GigOption option, bool isHidden)
    {
        return new Category(option.Id.ToString(), option.Name, option.Color, option.IsDefault, isHidden);
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();

    private sealed record OptionValues(string Name, string Color);

    private sealed record ListedOption<T>(T Option, bool IsHidden) where T : GigOption;

    private sealed class OptionKind<T>(
        IMongoCollection<T> collection,
        Func<GigSettings, ObjectId[]> hidden,
        Action<GigSettings, ObjectId[]> setHidden,
        Func<ObjectId, ObjectId, Task<bool>> isInUse) where T : GigOption
    {
        public IMongoCollection<T> Collection { get; } = collection;
        public Func<GigSettings, ObjectId[]> Hidden { get; } = hidden;
        public Action<GigSettings, ObjectId[]> SetHidden { get; } = setHidden;

        public Task<bool> IsInUseAsync(ObjectId userId, ObjectId optionId)
        {
            return isInUse(userId, optionId);
        }
    }
}
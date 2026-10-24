using MongoDB.Bson;
using SartainStudios.Api.Service.Gig;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Test.Gig;

public sealed class GigSourceServiceTests
{
    private static readonly DateTimeOffset NowOffset = new(new DateTime(2026, 8, 12, 18, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task SeedAsync_CreatesAllDefaultsAndIsIdempotent()
    {
        var fixture = CreateFixture();
        var seeder = new GigSeeder(fixture.Harness.Database, new StaticTimeProvider(NowOffset));

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(GigDefaults.Sources.Count, fixture.Harness.GigSources.Documents.Count);
        Assert.Equal(GigDefaults.Categories.Count, fixture.Harness.GigExpenseCategories.Documents.Count);
        Assert.All(fixture.Harness.GigSources.Documents, source =>
        {
            Assert.True(source.IsDefault);
            Assert.Null(source.UserId);
        });
        Assert.Contains(fixture.Harness.GigSources.Documents, source => source.Name == "Uber Eats");
        Assert.Contains(fixture.Harness.GigSources.Documents, source => source.Name == "Amazon Flex");
        Assert.Contains(fixture.Harness.GigExpenseCategories.Documents, category => category.Name == "Car Maintenance");
    }

    [Fact]
    public async Task ListSourcesAsync_ReturnsDefaultsPlusOnlyTheCallersOwn()
    {
        var fixture = await CreateSeededFixtureAsync();
        fixture.Harness.GigSources.Seed(
            new GigSource { UserId = fixture.UserId, Name = "My Gig" },
            new GigSource { UserId = ObjectId.GenerateNewId(), Name = "Someone Else's Gig" });

        var result = await fixture.Service.ListSourcesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(GigDefaults.Sources.Count + 1, result.Value.Count);
        Assert.Contains(result.Value, source => source.Name == "My Gig" && !source.IsDefault);
        Assert.DoesNotContain(result.Value, source => source.Name == "Someone Else's Gig");
        Assert.Equal("Uber Eats", result.Value[0].Name);
    }

    [Fact]
    public async Task CreateSourceAsync_AddsAPrivateSource()
    {
        var fixture = await CreateSeededFixtureAsync();

        var result = await fixture.Service.CreateSourceAsync(new OptionRequest("  Local Courier ", "#112233"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Local Courier", result.Value.Name);
        Assert.Equal("#112233", result.Value.Color);
        Assert.False(result.Value.IsDefault);
        var stored = fixture.Harness.GigSources.Documents.Single(source => !source.IsDefault);
        Assert.Equal(fixture.UserId, stored.UserId);
    }

    [Theory]
    [InlineData("doordash")]
    [InlineData("DOORDASH ")]
    public async Task CreateSourceAsync_RejectsANameThatDuplicatesADefault(string name)
    {
        var fixture = await CreateSeededFixtureAsync();

        var result = await fixture.Service.CreateSourceAsync(new OptionRequest(name, null));

        Assert.Equal(GigErrors.OptionNameTaken, result.Error);
    }

    [Fact]
    public async Task CreateSourceAsync_RejectsDuplicateOwnNamesButAllowsOtherUsersToReuseThem()
    {
        var fixture = await CreateSeededFixtureAsync();
        fixture.Harness.GigSources.Seed(new GigSource { UserId = ObjectId.GenerateNewId(), Name = "Local Courier" });
        await fixture.Service.CreateSourceAsync(new OptionRequest("Local Courier", null));

        var duplicate = await fixture.Service.CreateSourceAsync(new OptionRequest("local courier", null));

        Assert.Equal(GigErrors.OptionNameTaken, duplicate.Error);
        Assert.Equal(2, fixture.Harness.GigSources.Documents.Count(source => source.Name == "Local Courier"));
    }

    [Theory]
    [InlineData("", "name")]
    [InlineData("   ", "name")]
    [InlineData("A very long source name that goes well past the fifty character limit", "name")]
    public async Task CreateSourceAsync_ValidatesTheName(string name, string field)
    {
        var fixture = await CreateSeededFixtureAsync();

        var result = await fixture.Service.CreateSourceAsync(new OptionRequest(name, null));

        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.True(validation.Errors.ContainsKey(field));
    }

    [Fact]
    public async Task CreateSourceAsync_RejectsAnInvalidColor()
    {
        var fixture = await CreateSeededFixtureAsync();

        var result = await fixture.Service.CreateSourceAsync(new OptionRequest("Mine", "red"));

        Assert.Equal(GigErrors.ColorInvalid, result.Error);
    }

    [Fact]
    public async Task UpdateAndDeleteSourceAsync_CannotModifyDefaults()
    {
        var fixture = await CreateSeededFixtureAsync();
        var uber = fixture.Harness.GigSources.Documents.First(source => source.Name == "Uber Eats");

        var update = await fixture.Service.UpdateSourceAsync(uber.Id.ToString(), new OptionRequest("Renamed", null));
        var delete = await fixture.Service.DeleteSourceAsync(uber.Id.ToString());

        Assert.Equal(GigErrors.OptionIsDefault, update.Error);
        Assert.Equal(GigErrors.OptionIsDefault, delete.Error);
        Assert.Equal("Uber Eats", uber.Name);
    }

    [Fact]
    public async Task UpdateAndDeleteSourceAsync_CannotSeeAnotherUsersSource()
    {
        var fixture = await CreateSeededFixtureAsync();
        var foreign = new GigSource { UserId = ObjectId.GenerateNewId(), Name = "Theirs" };
        fixture.Harness.GigSources.Seed(foreign);

        var update = await fixture.Service.UpdateSourceAsync(foreign.Id.ToString(), new OptionRequest("Mine", null));
        var delete = await fixture.Service.DeleteSourceAsync(foreign.Id.ToString());
        var hide = await fixture.Service.SetSourceHiddenAsync(foreign.Id.ToString(), new HiddenRequest(true));

        Assert.Equal(GigErrors.OptionNotFound, update.Error);
        Assert.Equal(GigErrors.OptionNotFound, delete.Error);
        Assert.Equal(GigErrors.OptionNotFound, hide.Error);
    }

    [Fact]
    public async Task UpdateSourceAsync_RenamesAnOwnSource()
    {
        var fixture = await CreateSeededFixtureAsync();
        var created = await fixture.Service.CreateSourceAsync(new OptionRequest("Courier", null));

        var result =
            await fixture.Service.UpdateSourceAsync(created.Value.Id, new OptionRequest("Courier Co", "#abcdef"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Courier Co", result.Value.Name);
        Assert.Equal("#ABCDEF", result.Value.Color);
    }

    [Fact]
    public async Task DeleteSourceAsync_RemovesAnUnusedOwnSourceButBlocksOneInUse()
    {
        var fixture = await CreateSeededFixtureAsync();
        var unused = await fixture.Service.CreateSourceAsync(new OptionRequest("Unused", null));
        var used = await fixture.Service.CreateSourceAsync(new OptionRequest("Used", null));
        fixture.Harness.GigSessions.Seed(new GigSession
        {
            UserId = fixture.UserId,
            StartTime = NowOffset.UtcDateTime.AddDays(-1),
            EndTime = NowOffset.UtcDateTime.AddDays(-1).AddHours(1),
            Earnings = [new GigEarningEntry { SourceId = ObjectId.Parse(used.Value.Id), Amount = 5m }]
        });

        var deleteUnused = await fixture.Service.DeleteSourceAsync(unused.Value.Id);
        var deleteUsed = await fixture.Service.DeleteSourceAsync(used.Value.Id);

        Assert.True(deleteUnused.IsSuccess);
        Assert.Equal(GigErrors.OptionInUse, deleteUsed.Error);
        Assert.DoesNotContain(fixture.Harness.GigSources.Documents, source => source.Name == "Unused");
        Assert.Contains(fixture.Harness.GigSources.Documents, source => source.Name == "Used");
    }

    [Fact]
    public async Task SetSourceHiddenAsync_HidesAndUnhidesADefaultForOnlyTheCaller()
    {
        var fixture = await CreateSeededFixtureAsync();
        var lyft = fixture.Harness.GigSources.Documents.First(source => source.Name == "Lyft");

        var hidden = await fixture.Service.SetSourceHiddenAsync(lyft.Id.ToString(), new HiddenRequest(true));
        var listed = await fixture.Service.ListSourcesAsync();
        var otherUser = fixture.ServiceFor(ObjectId.GenerateNewId());
        var otherList = await otherUser.ListSourcesAsync();

        Assert.True(hidden.Value.IsHidden);
        Assert.True(listed.Value.Single(source => source.Name == "Lyft").IsHidden);
        Assert.False(otherList.Value.Single(source => source.Name == "Lyft").IsHidden);

        await fixture.Service.SetSourceHiddenAsync(lyft.Id.ToString(), new HiddenRequest(false));
        var restored = await fixture.Service.ListSourcesAsync();
        Assert.False(restored.Value.Single(source => source.Name == "Lyft").IsHidden);
    }

    [Fact]
    public async Task Categories_FollowTheSameRules()
    {
        var fixture = await CreateSeededFixtureAsync();
        var fuel = fixture.Harness.GigExpenseCategories.Documents.First(category => category.Name == "Fuel");

        var list = await fixture.Service.ListCategoriesAsync();
        var duplicate = await fixture.Service.CreateCategoryAsync(new OptionRequest("fuel", null));
        var created = await fixture.Service.CreateCategoryAsync(new OptionRequest("Bike Repairs", "#00ff00"));
        var editDefault = await fixture.Service.UpdateCategoryAsync(fuel.Id.ToString(), new OptionRequest("Gas", null));
        var hide = await fixture.Service.SetCategoryHiddenAsync(fuel.Id.ToString(), new HiddenRequest(true));
        var deleteCreated = await fixture.Service.DeleteCategoryAsync(created.Value.Id);

        Assert.Equal(GigDefaults.Categories.Count, list.Value.Count);
        Assert.Equal(GigErrors.OptionNameTaken, duplicate.Error);
        Assert.True(created.IsSuccess);
        Assert.Equal(GigErrors.OptionIsDefault, editDefault.Error);
        Assert.True(hide.Value.IsHidden);
        Assert.True(deleteCreated.IsSuccess);
    }

    [Fact]
    public async Task DeleteSourceAsync_AlsoClearsAnyHiddenFlag()
    {
        var fixture = await CreateSeededFixtureAsync();
        var created = await fixture.Service.CreateSourceAsync(new OptionRequest("Temp", null));
        await fixture.Service.SetSourceHiddenAsync(created.Value.Id, new HiddenRequest(true));

        await fixture.Service.DeleteSourceAsync(created.Value.Id);

        Assert.Empty(fixture.Harness.GigSettings.Documents.Single().HiddenSourceIds);
    }

    private static async Task<Fixture> CreateSeededFixtureAsync()
    {
        var fixture = CreateFixture();
        await new GigSeeder(fixture.Harness.Database, new StaticTimeProvider(NowOffset)).SeedAsync();
        return fixture;
    }

    private static Fixture CreateFixture()
    {
        var userId = ObjectId.GenerateNewId();
        var harness = new MongoHarness();
        return new Fixture(userId, harness, BuildService(harness, userId));
    }

    private static GigSourceService BuildService(MongoHarness harness, ObjectId userId)
    {
        var tenant = TestTenant.Create(userId);
        var timeProvider = new StaticTimeProvider(NowOffset);
        var settings = new GigSettingsService(harness.Database, tenant, timeProvider);
        return new GigSourceService(harness.Database, tenant, settings, timeProvider);
    }

    private sealed record Fixture(ObjectId UserId, MongoHarness Harness, GigSourceService Service)
    {
        public GigSourceService ServiceFor(ObjectId userId)
        {
            return BuildService(Harness, userId);
        }
    }
}
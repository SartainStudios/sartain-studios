using MongoDB.Bson;
using SartainStudios.Api.Service.Gig;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Test.Gig;

public sealed class GigSettingsServiceTests
{
    private static readonly DateTimeOffset NowOffset = new(new DateTime(2026, 8, 12, 18, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task GetAsync_DefaultsToMileageDeductionOff()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.GetAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.MileageDeductionEnabled);
        Assert.Null(result.Value.MileageRateOverride);
        Assert.Equal(2026, result.Value.RateYear);
        Assert.Equal(MileageRates.ForYear(2026), result.Value.EffectiveRate);
        Assert.Empty(fixture.Harness.GigSettings.Documents);
    }

    [Fact]
    public async Task UpdateAsync_PersistsTheTogglePerUser()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.UpdateAsync(new UpdatePreferencesRequest(true, null));
        var again = await fixture.Service.UpdateAsync(new UpdatePreferencesRequest(false, 0.6m));

        Assert.True(result.Value.MileageDeductionEnabled);
        Assert.False(again.Value.MileageDeductionEnabled);
        Assert.Equal(0.6m, again.Value.EffectiveRate);
        var stored = Assert.Single(fixture.Harness.GigSettings.Documents);
        Assert.Equal(fixture.UserId, stored.UserId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    public async Task UpdateAsync_RejectsAnInvalidRateOverride(double rate)
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.UpdateAsync(new UpdatePreferencesRequest(true, (decimal)rate));

        Assert.Equal(GigErrors.MileageRateInvalid, result.Error);
        Assert.Empty(fixture.Harness.GigSettings.Documents);
    }

    [Fact]
    public async Task GetAsync_RejectsAnUnresolvedUser()
    {
        var harness = new MongoHarness();
        var service =
            new GigSettingsService(harness.Database, TestTenant.Anonymous(), new StaticTimeProvider(NowOffset));

        var result = await service.GetAsync();

        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    private static Fixture CreateFixture()
    {
        var userId = ObjectId.GenerateNewId();
        var harness = new MongoHarness();
        var service = new GigSettingsService(harness.Database, TestTenant.Create(userId),
            new StaticTimeProvider(NowOffset));
        return new Fixture(userId, harness, service);
    }

    private sealed record Fixture(ObjectId UserId, MongoHarness Harness, GigSettingsService Service);
}
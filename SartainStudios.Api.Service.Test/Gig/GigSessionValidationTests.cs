using MongoDB.Bson;
using SartainStudios.Api.Service.Gig;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Test.Gig;

public sealed class GigSessionValidationTests
{
    private static readonly DateTime Now = new(2026, 8, 12, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Validate_AcceptsASessionWithNoEntries()
    {
        var result = Validate(Now.AddHours(-2), Now);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Earnings);
        Assert.Empty(result.Value.Expenses);
        Assert.Equal(nameof(VehicleType.Car), result.Value.VehicleType);
    }

    [Fact]
    public void Validate_RejectsEndBeforeStart()
    {
        var result = Validate(Now.AddHours(-1), Now.AddHours(-2));

        AssertFieldError(result, GigErrors.EndTimeField);
    }

    [Fact]
    public void Validate_AllowsEndEqualToStart()
    {
        Assert.True(Validate(Now.AddHours(-1), Now.AddHours(-1)).IsSuccess);
    }

    [Fact]
    public void Validate_RejectsEndOdometerBelowStartOdometer()
    {
        var result = Validate(Now.AddHours(-2), Now, 200m, 150m);

        AssertFieldError(result, GigErrors.EndOdometerField);
    }

    [Fact]
    public void Validate_RejectsNegativeOdometerReadings()
    {
        var result = Validate(Now.AddHours(-2), Now, -1m, -2m);

        AssertFieldError(result, GigErrors.StartOdometerField);
        AssertFieldError(result, GigErrors.EndOdometerField);
    }

    [Fact]
    public void Validate_RejectsNegativeAmounts()
    {
        var id = ObjectId.GenerateNewId().ToString();

        var result = Validate(
            Now.AddHours(-2),
            Now,
            earnings: [new EarningEntry(id, -5m, null)],
            expenses: [new ExpenseEntry(id, -1m, null)]);

        AssertFieldError(result, "earnings[0]");
        AssertFieldError(result, "expenses[0]");
    }

    [Fact]
    public void Validate_AcceptsZeroAmountsAndRoundsToCents()
    {
        var id = ObjectId.GenerateNewId().ToString();

        var result = Validate(
            Now.AddHours(-2),
            Now,
            earnings: [new EarningEntry(id, 0m, null), new EarningEntry(id, 12.345m, " tip ")]);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.Earnings[0].Amount);
        Assert.Equal(12.35m, result.Value.Earnings[1].Amount);
        Assert.Equal("tip", result.Value.Earnings[1].Note);
    }

    [Fact]
    public void Validate_RejectsInvalidSourceIdAndVehicleType()
    {
        var result = Validate(
            Now.AddHours(-2),
            Now,
            vehicleType: "Hovercraft",
            earnings: [new EarningEntry("nope", 1m, null)]);

        AssertFieldError(result, GigErrors.VehicleTypeField);
        AssertFieldError(result, "earnings[0]");
    }

    [Fact]
    public void Validate_RejectsNonUtcTimes()
    {
        var local = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Local);

        var result = Validate(local, null);

        AssertFieldError(result, GigErrors.StartTimeField);
    }

    [Fact]
    public void Validate_RejectsFutureTimes()
    {
        var result = Validate(Now.AddHours(1), Now.AddHours(2));

        AssertFieldError(result, GigErrors.StartTimeField);
        AssertFieldError(result, GigErrors.EndTimeField);
    }

    [Theory]
    [InlineData("Car", true)]
    [InlineData("motorcycle", true)]
    [InlineData("Bicycle", false)]
    [InlineData("EBike", false)]
    [InlineData("Other", false)]
    [InlineData(null, false)]
    public void VehicleTypes_OnlyMotorVehiclesAreDeductible(string? vehicleType, bool expected)
    {
        Assert.Equal(expected, VehicleTypes.IsDeductible(vehicleType));
    }

    private static Result<GigSessionValidation.ValidatedGigSession> Validate(
        DateTime start,
        DateTime? end,
        decimal? startOdometer = null,
        decimal? endOdometer = null,
        string? vehicleType = null,
        IReadOnlyList<EarningEntry>? earnings = null,
        IReadOnlyList<ExpenseEntry>? expenses = null)
    {
        return GigSessionValidation.Validate(
            start, end, startOdometer, endOdometer, vehicleType, null, earnings, expenses, Now);
    }

    private static void AssertFieldError(Result result, string field)
    {
        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.True(validation.Errors.ContainsKey(field), $"Expected a validation error for '{field}'.");
    }
}
using SartainStudios.Client.Service;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Test.Gig;

public sealed class EarningsScreenshotTests
{
    [Theory]
    [InlineData(EarningsScreenshotSource.Uber, GigDefaults.UberEatsSourceName, "uber-id")]
    [InlineData(EarningsScreenshotSource.DoorDash, GigDefaults.DoorDashSourceName, "doordash-id")]
    public void ScreenshotAppSelectsItsBuiltInEarningsSource(string platform, string name, string expectedId)
    {
        Source[] sources =
        [
            new("uber-id", GigDefaults.UberEatsSourceName, "", true, false),
            new("doordash-id", GigDefaults.DoorDashSourceName, "", true, false),
            new("other-id", "Other", "", true, false)
        ];

        var source = EarningsScreenshotSource.Find(platform, sources);

        Assert.NotNull(source);
        Assert.Equal(expectedId, source.Id);
        Assert.Equal(name, source.Name);
    }

    [Fact]
    public void ScreenshotAppDoesNotUseHiddenCustomAmbiguousOrUnsupportedSources()
    {
        Source[] hiddenOrCustom =
        [
            new("hidden", GigDefaults.UberEatsSourceName, "", true, true),
            new("custom", GigDefaults.UberEatsSourceName, "", false, false)
        ];
        Assert.Null(EarningsScreenshotSource.Find(EarningsScreenshotSource.Uber, hiddenOrCustom));
        Assert.Null(EarningsScreenshotSource.Find("Other", [new("other", "Other", "", true, false)]));
        Assert.Null(EarningsScreenshotSource.Find(EarningsScreenshotSource.DoorDash, []));
        Assert.Null(EarningsScreenshotSource.Find(EarningsScreenshotSource.Uber,
        [
            new("first", GigDefaults.UberEatsSourceName, "", true, false),
            new("second", GigDefaults.UberEatsSourceName, "", true, false)
        ]));
    }

    [Fact]
    public void UberReadsActivityAmountsNotTipsAndKeepsSameTimeOrders()
    {
        const string text = """
            Sat, Nov 7
            5.50
            0 $ 1:52 PM
            Delivery - 15 min 38 sec - 1.00 mi
            e $4.00 Tip
            6.50
            0 $ 1:31 PM
            e $5.00 Tip
            12.18
            =] 3 12:32 PM
            e $8.57 Tip
            8.62
            0 $ 12:30 PM
            11.77
            0 $ 12:30 PM
            7.50
            0 $ 11:48 AM
            $2.00
            9:28 AM
            Incentive
            """;

        var readings = EarningsScreenshotParser.ReadUber(text);

        Assert.Equal([5.50m, 6.50m, 12.18m, 8.62m, 11.77m, 7.50m, 2m], readings.Select(x => x.Amount));
        Assert.Equal(TimeSpan.FromHours(12.5), readings[3].Start);
        Assert.Equal(readings[3].Start, readings[4].Start);
        Assert.All(readings, x => Assert.Null(x.End));
    }

    [Fact]
    public void UberAcceptsOcrSymbolsBeforeAmountWithoutReadingTips()
    {
        const string text = """
            = $12.18 12:32 PM
            e $8.57 Tip
            0 $8.62 12:30 PM
            ©) $2.00 9:28 AM
            Incentive
            """;
        var readings = EarningsScreenshotParser.ReadUber(text);
        Assert.Equal([12.18m, 8.62m, 2m], readings.Select(x => x.Amount));
        Assert.Equal(new TimeSpan(9, 28, 0), readings[2].Start);
    }

    [Fact]
    public void DoorDashUsesShiftTotalNotOfferAmounts()
    {
        const string text = """
            Nov 7, 2026
            $13.45 v Earn per offer
            Start Time 11:45 AM
            End Time 1:06 PM
            Active time 29 min
            2 DoorDash Offers
            Restaurant $7.60
            Restaurant $5.85
            """;
        var reading = Assert.Single(EarningsScreenshotParser.ReadDoorDash(text));
        Assert.Equal(13.45m, reading.Amount);
        Assert.Equal(new TimeSpan(11, 45, 0), reading.Start);
        Assert.Equal(new TimeSpan(13, 6, 0), reading.End);
    }

    [Theory]
    [InlineData("$13.45\nStart Time 11:45 AM\nRestaurant $7.60")]
    [InlineData("Start Time 11:45 AM\nEnd Time 1:06 PM\nRestaurant $7.60")]
    public void DoorDashDoesNotGuessWhenRequiredInformationIsMissing(string text) =>
        Assert.Empty(EarningsScreenshotParser.ReadDoorDash(text));

    [Fact]
    public void MatchRequiresEntireDisplayedMinuteAndExactlyOneClosedSession()
    {
        var date = new DateTime(2026, 11, 7);
        var session = Session("1", date.AddHours(11), date.AddHours(13));
        Assert.Equal("1", EarningsSessionMatcher.Suggest(date, new TimeSpan(12, 30, 0), null, [session], TimeZoneInfo.Utc));
        Assert.Null(EarningsSessionMatcher.Suggest(date, new TimeSpan(13, 0, 0), null, [session], TimeZoneInfo.Utc));
        Assert.Null(EarningsSessionMatcher.Suggest(date, new TimeSpan(12, 30, 0), null,
            [session, Session("2", date.AddHours(12), date.AddHours(14))], TimeZoneInfo.Utc));
        Assert.Null(EarningsSessionMatcher.Suggest(date, new TimeSpan(12, 30, 0), null,
            [session with { IsOpen = true }], TimeZoneInfo.Utc));
    }

    [Fact]
    public void OvernightDashMustFitEntirelyInsideOneSession()
    {
        var date = new DateTime(2026, 11, 7);
        var session = Session("1", date.AddHours(22), date.AddDays(1).AddHours(3));
        Assert.Equal("1", EarningsSessionMatcher.Suggest(date, new TimeSpan(23, 0, 0), new TimeSpan(1, 0, 0),
            [session], TimeZoneInfo.Utc));
        Assert.Null(EarningsSessionMatcher.Suggest(date, new TimeSpan(23, 0, 0), new TimeSpan(4, 0, 0),
            [session], TimeZoneInfo.Utc));
        Assert.Null(EarningsSessionMatcher.Suggest(date, new TimeSpan(23, 0, 0), new TimeSpan(23, 0, 0),
            [session], TimeZoneInfo.Utc));
    }

    [Fact]
    public void AmbiguousDaylightSavingTimeIsNotSuggested()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        var date = new DateTime(2026, 11, 1);
        var session = Session("1", date.AddHours(5), date.AddHours(9));
        Assert.Null(EarningsSessionMatcher.Suggest(date, new TimeSpan(1, 30, 0), null, [session], zone));
    }

    private static GigSessionDto Session(string id, DateTime start, DateTime end) =>
        new(id, DateTime.SpecifyKind(start, DateTimeKind.Utc), DateTime.SpecifyKind(end, DateTimeKind.Utc),
            false, 0, null, null, null, "Car", true, "", [], [], 0, 0, 0, null, null);
}
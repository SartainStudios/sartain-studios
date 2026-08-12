using SartainStudios.Api.Service.Validation;

namespace SartainStudios.Api.Service.Test.Validation;

public sealed class EnumNameTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_ReturnsFalse_WhenNullOrWhitespace(string? value)
    {
        Assert.False(EnumName.TryNormalize<Color>(value, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryNormalize_ReturnsFalse_WhenValueNotInEnum()
    {
        Assert.False(EnumName.TryNormalize<Color>("Yellow", out var normalized));
        Assert.Equal(string.Empty, normalized);
    }

    [Theory]
    [InlineData("Red", "Red")]
    [InlineData("red", "Red")]
    [InlineData("RED", "Red")]
    [InlineData("  Green  ", "Green")]
    public void TryNormalize_ReturnsTrue_AndNormalizesCase(string input, string expected)
    {
        Assert.True(EnumName.TryNormalize<Color>(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Options_ReturnsAllEnumNames()
    {
        var result = EnumName.Options<Color>();
        Assert.Equal("Red, Green, Blue", result);
    }

    private enum Color
    {
        Red,
        Green,
        Blue
    }
}
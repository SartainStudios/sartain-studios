using SartainStudios.Api.Service.Validation;

namespace SartainStudios.Api.Service.Test.Validation;

public sealed class ContactTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidEmail_ReturnsFalse_WhenNullOrWhitespace(string? email)
    {
        Assert.False(Contact.IsValidEmail(email));
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@nodomain")]
    public void IsValidEmail_ReturnsFalse_WhenInvalidFormat(string email)
    {
        Assert.False(Contact.IsValidEmail(email));
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("  user@example.com  ")]
    [InlineData("user+tag@sub.domain.org")]
    public void IsValidEmail_ReturnsTrue_WhenValidEmail(string email)
    {
        Assert.True(Contact.IsValidEmail(email));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidPhoneNumber_ReturnsFalse_WhenNullOrWhitespace(string? phone)
    {
        Assert.False(Contact.IsValidPhoneNumber(phone));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("!!!!!")]
    public void IsValidPhoneNumber_ReturnsFalse_WhenInvalidFormat(string phone)
    {
        Assert.False(Contact.IsValidPhoneNumber(phone));
    }

    [Theory]
    [InlineData("+1 (555) 123-4567")]
    [InlineData("555-867-5309")]
    [InlineData("+441234567890")]
    public void IsValidPhoneNumber_ReturnsTrue_WhenValidPhone(string phone)
    {
        Assert.True(Contact.IsValidPhoneNumber(phone));
    }
}
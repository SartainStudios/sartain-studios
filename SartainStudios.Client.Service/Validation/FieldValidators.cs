using System.Text.RegularExpressions;

namespace SartainStudios.Client.Service.Validation;

public static partial class FieldValidators
{
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+?[0-9()\-.\s]{7,20}$")]
    private static partial Regex PhoneNumberPattern();

    [GeneratedRegex(@"^[A-Za-z0-9\-_]{1,20}$")]
    private static partial Regex InvoicePrefixPattern();

    public static string? ValidateRequiredEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "Email is required.";
        return EmailPattern().IsMatch(email.Trim()) ? null : "Enter a valid email address.";
    }

    public static string? ValidateOptionalEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        return EmailPattern().IsMatch(email.Trim()) ? null : "Enter a valid email address.";
    }

    public static string? ValidateRequiredPhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return "Phone number is required.";
        return PhoneNumberPattern().IsMatch(phoneNumber.Trim()) ? null : "Enter a valid phone number.";
    }

    public static string? ValidateOptionalPhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return null;
        return PhoneNumberPattern().IsMatch(phoneNumber.Trim()) ? null : "Enter a valid phone number.";
    }

    public static string? ValidateRequiredText(string? value, string label)
    {
        return string.IsNullOrWhiteSpace(value) ? $"{label} is required." : null;
    }

    public static string? ValidatePositiveAmount(decimal value, string label)
    {
        return value <= 0 ? $"{label} must be greater than zero." : null;
    }

    public static string? ValidateInvoicePrefix(string? invoicePrefix)
    {
        if (string.IsNullOrWhiteSpace(invoicePrefix)) return "Invoice prefix is required.";
        return InvoicePrefixPattern().IsMatch(invoicePrefix.Trim())
            ? null
            : "Use letters, digits, dashes or underscores only (e.g. 'SS-EC-').";
    }
}
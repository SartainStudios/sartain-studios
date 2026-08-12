using System.Net.Mail;
using System.Text.RegularExpressions;

namespace SartainStudios.Api.Service.Validation;

public static partial class Contact
{
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        try
        {
            _ = new MailAddress(email.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool IsValidPhoneNumber(string? phoneNumber)
    {
        return !string.IsNullOrWhiteSpace(phoneNumber) && PhoneNumberPattern().IsMatch(phoneNumber.Trim());
    }

    [GeneratedRegex(@"^\+?[0-9()\-.\s]{7,20}$")]
    private static partial Regex PhoneNumberPattern();
}
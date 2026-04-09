namespace Bebochka.Api.Utilities;

public static class PhoneAuthHelper
{
    /// <summary>
    /// Нормализация российского номера в E.164 (+7XXXXXXXXXX)
    /// </summary>
    public static string? NormalizeRuToE164(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith('8'))
            digits = "7" + digits[1..];
        if (digits.Length == 10 && digits.StartsWith('9'))
            digits = "7" + digits;
        if (digits.Length == 11 && digits.StartsWith('7'))
            return "+" + digits;
        return null;
    }
}

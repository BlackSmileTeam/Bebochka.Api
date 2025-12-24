namespace Bebochka.Api.Helpers;

/// <summary>
/// Helper class for working with Moscow time (UTC+3)
/// </summary>
public static class DateTimeHelper
{
    private static readonly TimeZoneInfo MoscowTimeZone = GetMoscowTimeZone();

    private static TimeZoneInfo GetMoscowTimeZone()
    {
        try
        {
            // Try Windows timezone ID first
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
            }
            catch
            {
                // Try Linux timezone ID
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
            }
        }
        catch
        {
            // Fallback: create custom timezone (UTC+3)
            return TimeZoneInfo.CreateCustomTimeZone("MSK", TimeSpan.FromHours(3), "Moscow Standard Time", "MSK");
        }
    }

    /// <summary>
    /// Gets current Moscow time
    /// </summary>
    public static DateTime GetMoscowTime()
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MoscowTimeZone);
        }
        catch
        {
            // Fallback: manually add 3 hours if timezone conversion fails
            return DateTime.UtcNow.AddHours(3);
        }
    }

    /// <summary>
    /// Converts UTC time to Moscow time
    /// </summary>
    public static DateTime ToMoscowTime(DateTime utcTime)
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, MoscowTimeZone);
        }
        catch
        {
            return utcTime.AddHours(3);
        }
    }

    /// <summary>
    /// Converts Moscow time to UTC time
    /// </summary>
    public static DateTime FromMoscowTime(DateTime moscowTime)
    {
        try
        {
            return TimeZoneInfo.ConvertTimeToUtc(moscowTime, MoscowTimeZone);
        }
        catch
        {
            return moscowTime.AddHours(-3);
        }
    }

    /// <summary>
    /// Parses a datetime string as Moscow time (format: "YYYY-MM-DDTHH:mm")
    /// </summary>
    public static DateTime? ParseMoscowTime(string? dateTimeString)
    {
        if (string.IsNullOrWhiteSpace(dateTimeString))
            return null;

        if (DateTime.TryParse(dateTimeString, out var parsedDate))
        {
            // The parsed date will be in local timezone or unspecified
            // We treat it as Moscow time, so we need to ensure it's in the right format
            // Since MySQL DATETIME doesn't store timezone, we'll store it as-is
            // and compare with Moscow time later
            return parsedDate;
        }

        return null;
    }
}


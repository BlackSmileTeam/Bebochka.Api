using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Bebochka.Api.Utilities;

public static class ClientInfoHelper
{
    public static string? GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext?.Request == null) return null;

        var headers = httpContext.Request.Headers;
        var forwarded = headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(first))
                return first.Length > 45 ? first[..45] : first;
        }

        var realIp = headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
            return realIp.Length > 45 ? realIp[..45] : realIp;

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    public static string ClassifyDevice(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return "Unknown";

        var ua = userAgent;
        if (ua.Contains("Tablet", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            return "Tablet";
        if (ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase) || ua.Contains("Android", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Tablet", StringComparison.OrdinalIgnoreCase))
            return "Mobile";
        if (ua.Contains("Windows", StringComparison.OrdinalIgnoreCase) || ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) || ua.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            return "Desktop";

        return "Unknown";
    }

    public static string? BuildExtraJson(HttpRequest? request)
    {
        if (request == null) return null;

        try
        {
            var o = new Dictionary<string, string?>();
            var lang = request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrWhiteSpace(lang))
                o["AcceptLanguage"] = lang.Length > 500 ? lang[..500] : lang;

            var referer = request.Headers.Referer.ToString();
            if (!string.IsNullOrWhiteSpace(referer))
                o["Referer"] = referer.Length > 1000 ? referer[..1000] : referer;

            var xff = request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(xff))
                o["XForwardedFor"] = xff.Length > 500 ? xff[..500] : xff;

            var secCh = request.Headers["Sec-CH-UA"].ToString();
            if (!string.IsNullOrWhiteSpace(secCh))
                o["SecChUa"] = secCh.Length > 500 ? secCh[..500] : secCh;

            if (o.Count == 0) return null;
            return JsonSerializer.Serialize(o);
        }
        catch
        {
            return null;
        }
    }
}

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

        var cfIp = headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cfIp))
            return cfIp.Length > 45 ? cfIp[..45] : cfIp;

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

    public static string? BuildExtraJson(HttpRequest? request, IReadOnlyDictionary<string, string?>? additional = null)
    {
        if (request == null && (additional == null || additional.Count == 0)) return null;

        try
        {
            var o = new Dictionary<string, string?>();

            void Put(string key, string? value, int maxLen = 1000)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                o[key] = value.Length > maxLen ? value[..maxLen] : value;
            }

            if (request != null)
            {
                Put("AcceptLanguage", request.Headers.AcceptLanguage.ToString(), 500);
                Put("Referer", request.Headers.Referer.ToString());
                Put("XForwardedFor", request.Headers["X-Forwarded-For"].ToString(), 500);
                Put("XRealIp", request.Headers["X-Real-IP"].ToString(), 45);
                Put("CfConnectingIp", request.Headers["CF-Connecting-IP"].ToString(), 45);
                Put("SecChUa", request.Headers["Sec-CH-UA"].ToString(), 500);
                Put("SecChUaMobile", request.Headers["Sec-CH-UA-Mobile"].ToString(), 64);
                Put("SecChUaPlatform", request.Headers["Sec-CH-UA-Platform"].ToString(), 128);
                Put("RequestHost", request.Host.Value, 255);
                Put("RequestPath", request.Path.Value, 500);
                Put("RequestMethod", request.Method, 16);
                Put("RequestScheme", request.Scheme, 16);
                if (request.HttpContext.Connection.RemotePort > 0)
                    o["RemotePort"] = request.HttpContext.Connection.RemotePort.ToString();
            }

            if (additional != null)
            {
                foreach (var (key, value) in additional)
                    Put(key, value);
            }

            if (o.Count == 0) return null;
            return JsonSerializer.Serialize(o);
        }
        catch
        {
            return null;
        }
    }
}

using System.Text.RegularExpressions;

namespace Bebochka.Api.Helpers;

/// <summary>
/// URL helpers for SEO (must match frontend buildProductPath / slugifyProductName).
/// </summary>
public static class SeoUrlHelper
{
    private static readonly Regex NonSlugChars = new(@"[^a-zа-я0-9]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RepeatedDashes = new(@"-+", RegexOptions.Compiled);

    public static string SlugifyProductName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var lower = name.ToLowerInvariant();
        var slug = NonSlugChars.Replace(lower, "-");
        slug = RepeatedDashes.Replace(slug, "-").Trim('-');
        return slug;
    }

    public static string BuildProductPath(int id, string? name)
    {
        var slug = SlugifyProductName(name);
        return string.IsNullOrEmpty(slug) ? $"/product/{id}" : $"/product/{id}-{slug}";
    }
}

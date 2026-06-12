using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bebochka.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bebochka.Api.Controllers;

[ApiController]
[Route("api/seo")]
public class SeoController : ControllerBase
{
    private static readonly string[] SiteUrls =
    {
        "https://bebochka.ru",
        "https://www.bebochka.online"
    };
    private readonly AppDbContext _db;

    public SeoController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Sitemap только для товаров в наличии (не проданных).</summary>
    [HttpGet("sitemap-products.xml")]
    [Produces("application/xml")]
    public async Task<IActionResult> SitemapProducts(CancellationToken cancellationToken)
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.QuantityInStock > 0)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new { p.Id, p.Name, p.UpdatedAt })
            .ToListAsync(cancellationToken);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urlset = new XElement(ns + "urlset");

        foreach (var p in products)
        {
            var slug = SlugifyProductName(p.Name);
            var path = string.IsNullOrEmpty(slug) ? $"product/{p.Id}" : $"product/{p.Id}-{slug}";
            var lastmod = p.UpdatedAt.ToString("yyyy-MM-dd");

            foreach (var siteUrl in SiteUrls)
            {
                urlset.Add(
                    new XElement(ns + "url",
                        new XElement(ns + "loc", $"{siteUrl}/{path}"),
                        new XElement(ns + "lastmod", lastmod),
                        new XElement(ns + "changefreq", "weekly"),
                        new XElement(ns + "priority", "0.7")));
            }
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);
        using var ms = new MemoryStream();
        doc.Save(ms);
        return File(ms.ToArray(), "application/xml; charset=utf-8");
    }

    private static string SlugifyProductName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var lower = name.ToLowerInvariant();
        var slug = Regex.Replace(lower, @"[^a-zа-яё0-9]+", "-");
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');
        return slug;
    }
}

using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Helpers;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Public SEO endpoints (sitemaps for search engines).
/// </summary>
[ApiController]
[Route("api/seo")]
[AllowAnonymous]
public class SeoController : ControllerBase
{
    private const string SiteBase = "https://bebochka.ru";
    private readonly AppDbContext _context;

    public SeoController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Sitemap of published product pages (same visibility rules as the public catalog).
    /// </summary>
    [HttpGet("sitemap-products.xml")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetProductsSitemap(CancellationToken cancellationToken)
    {
        var moscowNow = DateTimeHelper.GetMoscowTime();
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.PublishedAt == null || p.PublishedAt <= moscowNow)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new { p.Id, p.Name, p.UpdatedAt })
            .ToListAsync(cancellationToken);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urlset = new XElement(
            ns + "urlset",
            products.Select(p =>
            {
                var path = SeoUrlHelper.BuildProductPath(p.Id, p.Name);
                return new XElement(
                    ns + "url",
                    new XElement(ns + "loc", SiteBase + path),
                    new XElement(ns + "lastmod", p.UpdatedAt.ToString("yyyy-MM-dd")),
                    new XElement(ns + "changefreq", "weekly"),
                    new XElement(ns + "priority", "0.7"));
            }));

        var xml = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);
        return Content(xml.ToString(), "application/xml", Encoding.UTF8);
    }
}

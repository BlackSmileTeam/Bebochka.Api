using System.Security.Cryptography;
using System.Text;
using Bebochka.Api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Уменьшенные копии фото для каталога и превью — быстрее на мобильном интернете.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class MediaController : ControllerBase
{
    private const int MinWidth = 64;
    private const int MaxWidth = 1600;
    private const int DefaultWidth = 480;
    private const int JpegQuality = 82;

    private readonly IWebHostEnvironment _environment;

    public MediaController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>GET /api/media/thumb?path=/uploads/file.jpg&amp;w=480</summary>
    [HttpGet("thumb")]
    [ResponseCache(Duration = 86400 * 30, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetThumbnail(
        [FromQuery] string path,
        [FromQuery] int w = DefaultWidth,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest("path is required");

        var normalized = path.Trim().Replace('\\', '/');
        if (!normalized.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return BadRequest("only /uploads/ paths are allowed");

        var fileName = Path.GetFileName(normalized);
        if (string.IsNullOrEmpty(fileName) || fileName is "." or "..")
            return BadRequest("invalid path");

        var width = Math.Clamp(w, MinWidth, MaxWidth);
        var wwwroot = AppPaths.WwwRoot(_environment);
        var sourcePath = Path.GetFullPath(Path.Combine(wwwroot, normalized.TrimStart('/')));

        if (!sourcePath.StartsWith(Path.GetFullPath(wwwroot), StringComparison.OrdinalIgnoreCase))
            return BadRequest("invalid path");

        if (!System.IO.File.Exists(sourcePath))
            return NotFound();

        var cacheDir = Path.Combine(wwwroot, "cache", "thumbs");
        Directory.CreateDirectory(cacheDir);

        var cacheKey = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{normalized}|{width}|{JpegQuality}")))[..32];
        var cachePath = Path.Combine(cacheDir, $"{cacheKey}.jpg");

        try
        {
            var sourceModified = System.IO.File.GetLastWriteTimeUtc(sourcePath);
            if (System.IO.File.Exists(cachePath))
            {
                var cacheModified = System.IO.File.GetLastWriteTimeUtc(cachePath);
                if (cacheModified >= sourceModified)
                    return PhysicalFile(cachePath, "image/jpeg");
            }

            await using var input = System.IO.File.OpenRead(sourcePath);
            using var image = await Image.LoadAsync(input, cancellationToken);

            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(width, 0),
                Mode = ResizeMode.Max,
            }));

            var encoder = new JpegEncoder { Quality = JpegQuality };
            await using (var output = System.IO.File.Create(cachePath))
            {
                await image.SaveAsync(output, encoder, cancellationToken);
            }

            return PhysicalFile(cachePath, "image/jpeg");
        }
        catch (UnknownImageFormatException)
        {
            return BadRequest("unsupported image format");
        }
    }
}

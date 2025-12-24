using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

namespace Bebochka.Api.Services;

/// <summary>
/// Service for creating image collages from product images
/// </summary>
public class CollageService
{
    private readonly ILogger<CollageService> _logger;
    private readonly IWebHostEnvironment _environment;

    public CollageService(ILogger<CollageService> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Creates a collage from up to 4 images (2x2 grid)
    /// </summary>
    /// <param name="imagePaths">List of image paths (relative to wwwroot)</param>
    /// <returns>Path to the created collage image</returns>
    public async Task<string> CreateCollageAsync(List<string> imagePaths)
    {
        if (imagePaths == null || imagePaths.Count == 0)
        {
            throw new ArgumentException("Image paths cannot be empty", nameof(imagePaths));
        }

        // Limit to 4 images
        var imagesToProcess = imagePaths.Take(4).ToList();
        
        var webRootPath = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsFolder = Path.Combine(webRootPath, "uploads");
        
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        // Load images
        var images = new List<Image>();
        try
        {
            foreach (var imagePath in imagesToProcess)
            {
                var fullPath = Path.Combine(webRootPath, imagePath.TrimStart('/'));
                if (File.Exists(fullPath))
                {
                    var image = await Image.LoadAsync(fullPath);
                    images.Add(image);
                }
                else
                {
                    _logger.LogWarning("Image not found: {ImagePath}", fullPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading images for collage");
            throw;
        }

        if (images.Count == 0)
        {
            throw new InvalidOperationException("No valid images found for collage");
        }

        // Calculate grid size (2x2 for up to 4 images)
        int gridCols = images.Count <= 2 ? images.Count : 2;
        int gridRows = images.Count <= 2 ? 1 : 2;
        
        // Size of each cell in the collage (square, 800x800 pixels each)
        const int cellSize = 800;
        const int padding = 10; // Padding between images
        
        int collageWidth = gridCols * cellSize + (gridCols - 1) * padding;
        int collageHeight = gridRows * cellSize + (gridRows - 1) * padding;

        // Create collage image
        using var collage = new Image<Rgba32>(collageWidth, collageHeight, Color.White);

        // Paste images into grid
        for (int i = 0; i < images.Count; i++)
        {
            int row = i / gridCols;
            int col = i % gridCols;
            
            int x = col * (cellSize + padding);
            int y = row * (cellSize + padding);

            var image = images[i];
            
            // Resize image to fit cell while maintaining aspect ratio
            var resized = image.Clone(img => img.Resize(new ResizeOptions
            {
                Size = new Size(cellSize, cellSize),
                Mode = ResizeMode.Max
            }));

            // Calculate position to center the image in the cell
            int offsetX = x + (cellSize - resized.Width) / 2;
            int offsetY = y + (cellSize - resized.Height) / 2;

            collage.Mutate(ctx => ctx.DrawImage(resized, new Point(offsetX, offsetY), 1f));
            
            resized.Dispose();
            image.Dispose();
        }

        // Save collage
        var collageFileName = $"collage_{Guid.NewGuid()}.jpg";
        var collagePath = Path.Combine(uploadsFolder, collageFileName);
        var collageRelativePath = $"/uploads/{collageFileName}";

        await collage.SaveAsJpegAsync(collagePath);
        
        _logger.LogInformation("Collage created: {CollagePath} with {ImageCount} images", collageRelativePath, images.Count);

        return collageRelativePath;
    }
}


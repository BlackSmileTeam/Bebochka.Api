using Bebochka.Api.Data;
using Bebochka.Api.Helpers;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Bebochka.Api.Services;

public class ProductKitService : IProductKitService
{
    public const string CartAddModePart = "part";
    public const string CartAddModeBundle = "bundle";

    private readonly AppDbContext _context;

    public ProductKitService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ProductDto> CreateKitAsync(CreateProductDto dto, List<string> imagePaths)
    {
        if (dto.KitParts == null || dto.KitParts.Count == 0)
            throw new InvalidOperationException("Для комплекта укажите хотя бы одну вещь в составе.");

        var kit = new ProductKit
        {
            KitPrice = dto.Price,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.ProductKits.Add(kit);
        await _context.SaveChangesAsync();

        var display = BuildProductFromDto(dto, imagePaths);
        display.KitId = kit.Id;
        display.IsKitDisplay = true;
        display.KitPartName = null;
        display.KitPartSortOrder = 0;
        display.Price = dto.Price;
        _context.Products.Add(display);

        var sort = 0;
        foreach (var part in dto.KitParts)
        {
            if (string.IsNullOrWhiteSpace(part.Name))
                throw new InvalidOperationException("Укажите название для каждой вещи комплекта.");
            if (part.Price <= 0)
                throw new InvalidOperationException("Цена каждой вещи комплекта должна быть больше 0.");

            var partProduct = BuildProductFromDto(dto, imagePaths);
            partProduct.Name = part.Name.Trim();
            partProduct.KitId = kit.Id;
            partProduct.IsKitDisplay = false;
            partProduct.KitPartName = part.Name.Trim();
            partProduct.KitPartSortOrder = sort++;
            partProduct.Price = part.Price;
            partProduct.Images = new List<string>();
            _context.Products.Add(partProduct);
        }

        await _context.SaveChangesAsync();
        return await MapDisplayProductDtoAsync(display, kit);
    }

    public async Task<ProductDto?> UpdateKitAsync(int displayProductId, UpdateProductDto dto, List<string> imagePaths)
    {
        var display = await _context.Products.FirstOrDefaultAsync(p => p.Id == displayProductId);
        if (display == null || !display.KitId.HasValue || !display.IsKitDisplay)
            return null;

        var kit = await _context.ProductKits.FindAsync(display.KitId.Value);
        if (kit == null)
            return null;

        if (dto.Price > 0)
            kit.KitPrice = dto.Price;
        kit.UpdatedAt = DateTime.UtcNow;

        ApplyDtoToProduct(display, dto, imagePaths);
        display.Price = kit.KitPrice;

        var existingParts = await _context.Products
            .Where(p => p.KitId == kit.Id && !p.IsKitDisplay)
            .ToListAsync();

        var incoming = dto.KitParts ?? new List<ProductKitPartInputDto>();
        if (dto.IsKit && incoming.Count == 0)
            throw new InvalidOperationException("Для комплекта укажите хотя бы одну вещь в составе.");

        var keepIds = incoming.Where(p => p.Id.HasValue).Select(p => p.Id!.Value).ToHashSet();
        foreach (var old in existingParts)
        {
            if (!keepIds.Contains(old.Id))
                _context.Products.Remove(old);
        }

        var sort = 0;
        foreach (var part in incoming)
        {
            if (string.IsNullOrWhiteSpace(part.Name))
                throw new InvalidOperationException("Укажите название для каждой вещи комплекта.");
            if (part.Price <= 0)
                throw new InvalidOperationException("Цена каждой вещи комплекта должна быть больше 0.");

            Product partProduct;
            if (part.Id.HasValue)
            {
                partProduct = existingParts.FirstOrDefault(p => p.Id == part.Id.Value)
                    ?? throw new InvalidOperationException($"Часть комплекта {part.Id} не найдена.");
                partProduct.Name = part.Name.Trim();
                partProduct.KitPartName = part.Name.Trim();
                partProduct.Price = part.Price;
                partProduct.KitPartSortOrder = sort++;
                ApplyDtoToProduct(partProduct, dto, new List<string>(), skipImages: true);
            }
            else
            {
                partProduct = BuildProductFromUpdateDto(dto, new List<string>());
                partProduct.Name = part.Name.Trim();
                partProduct.KitId = kit.Id;
                partProduct.IsKitDisplay = false;
                partProduct.KitPartName = part.Name.Trim();
                partProduct.KitPartSortOrder = sort++;
                partProduct.Price = part.Price;
                partProduct.Images = new List<string>();
                _context.Products.Add(partProduct);
            }
        }

        await _context.SaveChangesAsync();
        return await MapDisplayProductDtoAsync(display, kit);
    }

    public async Task<bool> DeleteKitByProductIdAsync(int productId)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product?.KitId == null)
            return false;

        var kitId = product.KitId.Value;
        var kitProducts = await _context.Products.Where(p => p.KitId == kitId).ToListAsync();
        var ids = kitProducts.Select(p => p.Id).ToList();

        var cartRows = await _context.CartItems.Where(c => ids.Contains(c.ProductId)).ToListAsync();
        if (cartRows.Count > 0)
            _context.CartItems.RemoveRange(cartRows);

        _context.Products.RemoveRange(kitProducts);
        var kit = await _context.ProductKits.FindAsync(kitId);
        if (kit != null)
            _context.ProductKits.Remove(kit);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ProductKitOptionsDto?> GetKitOptionsAsync(int productId, string? sessionId, int? currentUserId)
    {
        var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId);
        if (product?.KitId == null)
            return null;

        var kitId = product.KitId.Value;
        var kit = await _context.ProductKits.AsNoTracking().FirstOrDefaultAsync(k => k.Id == kitId);
        if (kit == null)
            return null;

        var display = await _context.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.KitId == kitId && p.IsKitDisplay)
            ?? product;

        var parts = await _context.Products.AsNoTracking()
            .Where(p => p.KitId == kitId && !p.IsKitDisplay)
            .OrderBy(p => p.KitPartSortOrder)
            .ThenBy(p => p.Id)
            .ToListAsync();

        var allIds = parts.Select(p => p.Id).ToList();
        var reserved = await GetReservedByOthersAsync(allIds, sessionId, currentUserId);
        var myCart = await GetMyCartByProductAsync(allIds, sessionId, currentUserId);

        var partDtos = parts.Select(p =>
        {
            myCart.TryGetValue(p.Id, out var myQty);
            return new ProductKitPartDto
            {
                ProductId = p.Id,
                PartName = p.KitPartName ?? p.Name,
                Price = p.Price,
                SortOrder = p.KitPartSortOrder,
                IsReservedByOthers = reserved.Contains(p.Id),
                InMyCart = myQty > 0,
                QuantityInMyCart = myQty,
            };
        }).ToList();

        partDtos = partDtos
            .OrderBy(p => p.IsReservedByOthers ? 1 : 0)
            .ThenBy(p => p.SortOrder)
            .ToList();

        var hasReservation = partDtos.Any(p => p.IsReservedByOthers);
        var canAddFull = partDtos.Count > 0 && partDtos.All(p => !p.IsReservedByOthers);

        return new ProductKitOptionsDto
        {
            KitId = kitId,
            DisplayProductId = display.Id,
            KitPrice = kit.KitPrice,
            DisplayName = display.Name,
            PartCount = partDtos.Count,
            HasKitReservation = hasReservation,
            CanAddFullKit = canAddFull,
            Parts = partDtos,
        };
    }

    public async Task EnrichProductDtoAsync(ProductDto dto, string? sessionId, int? currentUserId)
    {
        if (!dto.KitId.HasValue)
        {
            dto.IsKit = false;
            return;
        }

        dto.IsKit = true;
        var kit = await _context.ProductKits.AsNoTracking().FirstOrDefaultAsync(k => k.Id == dto.KitId);
        dto.KitPrice = kit?.KitPrice;

        var options = await GetKitOptionsAsync(dto.Id, sessionId, currentUserId);
        if (options != null)
            dto.KitParts = options.Parts;
    }

    public async Task<List<int>> GetKitProductIdsAsync(int kitId)
    {
        return await _context.Products
            .Where(p => p.KitId == kitId)
            .Select(p => p.Id)
            .ToListAsync();
    }

    internal static bool IsCatalogVisible(Product p) => !p.KitId.HasValue || p.IsKitDisplay;

    private static Product BuildProductFromDto(CreateProductDto dto, List<string> imagePaths)
    {
        return new Product
        {
            Name = dto.Name,
            Brand = dto.Brand,
            Description = dto.Description,
            Price = dto.Price,
            Size = dto.Size,
            Color = dto.Color,
            Images = imagePaths,
            QuantityInStock = dto.QuantityInStock > 0 ? dto.QuantityInStock : 1,
            Gender = dto.Gender,
            Condition = dto.Condition,
            Nuance = string.IsNullOrWhiteSpace(dto.Nuance) ? null : dto.Nuance.Trim(),
            DiscountPercent = NormalizeDiscountPercent(dto.DiscountPercent),
            PublishedAt = dto.PublishedAt,
            CartAvailableAt = dto.CartAvailableAt,
            BoxNumber = dto.BoxNumber,
            Owner = dto.Owner,
            IncomingShipmentId = dto.IncomingShipmentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    private static Product BuildProductFromUpdateDto(UpdateProductDto dto, List<string> imagePaths)
    {
        return new Product
        {
            Name = dto.Name,
            Brand = dto.Brand,
            Description = dto.Description,
            Price = dto.Price,
            Size = dto.Size,
            Color = dto.Color,
            Images = imagePaths,
            QuantityInStock = dto.QuantityInStock > 0 ? dto.QuantityInStock : 1,
            Gender = dto.Gender,
            Condition = dto.Condition,
            Nuance = string.IsNullOrWhiteSpace(dto.Nuance) ? null : dto.Nuance.Trim(),
            DiscountPercent = NormalizeDiscountPercent(dto.DiscountPercent),
            PublishedAt = dto.PublishedAt,
            CartAvailableAt = dto.CartAvailableAt,
            BoxNumber = dto.BoxNumber,
            IncomingShipmentId = dto.IncomingShipmentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    private static void ApplyDtoToProduct(Product product, UpdateProductDto dto, List<string> imagePaths, bool skipImages = false)
    {
        if (!string.IsNullOrEmpty(dto.Name))
            product.Name = dto.Name;
        if (dto.Brand != null)
            product.Brand = dto.Brand;
        if (dto.Description != null)
            product.Description = dto.Description;
        if (dto.Size != null)
            product.Size = dto.Size;
        if (dto.Color != null)
            product.Color = dto.Color;
        if (!skipImages && imagePaths.Count > 0)
            product.Images = imagePaths;
        if (dto.QuantityInStock > 0)
            product.QuantityInStock = dto.QuantityInStock;
        if (dto.Gender != null)
            product.Gender = dto.Gender;
        if (dto.Condition != null)
            product.Condition = dto.Condition;
        if (dto.Nuance != null)
            product.Nuance = string.IsNullOrWhiteSpace(dto.Nuance) ? null : dto.Nuance.Trim();
        if (dto.DiscountPercent.HasValue)
            product.DiscountPercent = NormalizeDiscountPercent(dto.DiscountPercent);
        if (dto.PublishedAt.HasValue)
        {
            product.PublishedAt = new DateTime(
                dto.PublishedAt.Value.Year,
                dto.PublishedAt.Value.Month,
                dto.PublishedAt.Value.Day,
                dto.PublishedAt.Value.Hour,
                dto.PublishedAt.Value.Minute,
                dto.PublishedAt.Value.Second,
                DateTimeKind.Unspecified);
        }
        else
            product.PublishedAt = null;

        if (dto.CartAvailableAt.HasValue)
        {
            product.CartAvailableAt = new DateTime(
                dto.CartAvailableAt.Value.Year,
                dto.CartAvailableAt.Value.Month,
                dto.CartAvailableAt.Value.Day,
                dto.CartAvailableAt.Value.Hour,
                dto.CartAvailableAt.Value.Minute,
                dto.CartAvailableAt.Value.Second,
                DateTimeKind.Unspecified);
        }
        else
            product.CartAvailableAt = null;

        if (dto.BoxNumber != null)
            product.BoxNumber = dto.BoxNumber;
        product.IncomingShipmentId = dto.IncomingShipmentId;
        product.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<ProductDto> MapDisplayProductDtoAsync(Product display, ProductKit kit)
    {
        var moscowNow = DateTimeHelper.GetMoscowTime();
        var cartUnlocked = !display.CartAvailableAt.HasValue || display.CartAvailableAt.Value <= moscowNow;
        var dto = new ProductDto
        {
            Id = display.Id,
            Name = display.Name,
            Brand = display.Brand,
            Description = display.Description,
            Price = display.Price,
            Size = display.Size,
            Color = display.Color,
            Images = display.Images,
            QuantityInStock = display.QuantityInStock,
            Gender = display.Gender,
            Condition = display.Condition,
            Nuance = display.Nuance,
            DiscountPercent = display.DiscountPercent,
            FinalPrice = ComputeFinalPrice(display.Price, display.DiscountPercent),
            PublishedAt = display.PublishedAt,
            CartAvailableAt = display.CartAvailableAt,
            CartUnlocked = cartUnlocked,
            BoxNumber = display.BoxNumber,
            Owner = display.Owner,
            IncomingShipmentId = display.IncomingShipmentId,
            CreatedAt = display.CreatedAt,
            UpdatedAt = display.UpdatedAt,
            IsKit = true,
            KitId = kit.Id,
            KitPrice = kit.KitPrice,
        };

        var parts = await _context.Products.AsNoTracking()
            .Where(p => p.KitId == kit.Id && !p.IsKitDisplay)
            .OrderBy(p => p.KitPartSortOrder)
            .Select(p => new ProductKitPartDto
            {
                ProductId = p.Id,
                PartName = p.KitPartName ?? p.Name,
                Price = p.Price,
                SortOrder = p.KitPartSortOrder,
            })
            .ToListAsync();
        dto.KitParts = parts;
        return dto;
    }

    private async Task<HashSet<int>> GetReservedByOthersAsync(List<int> productIds, string? sessionId, int? currentUserId)
    {
        if (productIds.Count == 0)
            return new HashSet<int>();

        var query = _context.CartItems.Where(c => productIds.Contains(c.ProductId));
        if (currentUserId.HasValue)
            query = query.Where(c => c.UserId == null || c.UserId != currentUserId.Value);
        else if (!string.IsNullOrEmpty(sessionId))
            query = query.Where(c => c.UserId != null || c.SessionId != sessionId);
        else
            return new HashSet<int>();

        var ids = await query.Select(c => c.ProductId).Distinct().ToListAsync();
        return ids.ToHashSet();
    }

    private async Task<Dictionary<int, int>> GetMyCartByProductAsync(List<int> productIds, string? sessionId, int? currentUserId)
    {
        if (productIds.Count == 0)
            return new Dictionary<int, int>();

        var query = _context.CartItems.Where(c => productIds.Contains(c.ProductId));
        if (currentUserId.HasValue)
            query = query.Where(c => c.UserId == currentUserId);
        else if (!string.IsNullOrEmpty(sessionId))
            query = query.Where(c => c.UserId == null && c.SessionId == sessionId);
        else
            return new Dictionary<int, int>();

        return await query
            .GroupBy(c => c.ProductId)
            .Select(g => new { g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty);
    }

    private static int? NormalizeDiscountPercent(int? percent)
    {
        if (!percent.HasValue || percent.Value <= 0)
            return null;
        if (percent.Value > 99)
            return 99;
        return percent.Value;
    }

    private static decimal? ComputeFinalPrice(decimal price, int? discountPercent)
    {
        if (!discountPercent.HasValue || discountPercent.Value <= 0)
            return null;
        return Math.Round(price * (100 - discountPercent.Value) / 100m, 2);
    }
}

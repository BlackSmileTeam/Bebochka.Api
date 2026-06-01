using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Services;

public class LookupItemsService
{
    private readonly AppDbContext _context;

    public LookupItemsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<LookupItemDto>> GetColorsAsync(string? search, CancellationToken ct = default)
    {
        var query = _context.ProductColors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search.Trim()));
        var items = await query.OrderBy(c => c.Name).ToListAsync(ct);
        var counts = await BuildCountMapAsync(p => p.Color, ct);
        return MapColors(items, counts);
    }

    public async Task<List<LookupItemDto>> GetConditionsAsync(string? search, CancellationToken ct = default)
    {
        var query = _context.ProductConditions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search.Trim()));
        var items = await query.OrderBy(c => c.Name).ToListAsync(ct);
        var counts = await BuildCountMapAsync(p => p.Condition, ct);
        return MapConditions(items, counts);
    }

    public async Task<List<LookupItemDto>> GetNuancesAsync(string? search, CancellationToken ct = default)
    {
        var query = _context.ProductNuances.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search.Trim()));
        var items = await query.OrderBy(c => c.Name).ToListAsync(ct);
        var counts = await BuildCountMapAsync(p => p.Nuance, ct);
        return MapNuances(items, counts);
    }

    public async Task<LookupItemDto> CreateColorAsync(string name, CancellationToken ct = default)
    {
        var normalized = NormalizeName(name);
        if (await _context.ProductColors.AnyAsync(c => c.Name.ToLower() == normalized.ToLower(), ct))
            throw new InvalidOperationException("Такое значение уже есть");
        var entity = new ProductColor { Name = normalized };
        _context.ProductColors.Add(entity);
        await _context.SaveChangesAsync(ct);
        return new LookupItemDto { Id = entity.Id, Name = entity.Name, ProductCount = 0 };
    }

    public async Task<LookupItemDto> CreateConditionAsync(string name, CancellationToken ct = default)
    {
        var normalized = NormalizeName(name);
        if (await _context.ProductConditions.AnyAsync(c => c.Name.ToLower() == normalized.ToLower(), ct))
            throw new InvalidOperationException("Такое значение уже есть");
        var entity = new ProductCondition { Name = normalized };
        _context.ProductConditions.Add(entity);
        await _context.SaveChangesAsync(ct);
        return new LookupItemDto { Id = entity.Id, Name = entity.Name, ProductCount = 0 };
    }

    public async Task<LookupItemDto> CreateNuanceAsync(string name, CancellationToken ct = default)
    {
        var normalized = NormalizeName(name);
        if (await _context.ProductNuances.AnyAsync(c => c.Name.ToLower() == normalized.ToLower(), ct))
            throw new InvalidOperationException("Такое значение уже есть");
        var entity = new ProductNuance { Name = normalized };
        _context.ProductNuances.Add(entity);
        await _context.SaveChangesAsync(ct);
        return new LookupItemDto { Id = entity.Id, Name = entity.Name, ProductCount = 0 };
    }

    public async Task<LookupItemDto?> UpdateColorAsync(int id, string name, CancellationToken ct = default)
    {
        var entity = await _context.ProductColors.FindAsync(new object[] { id }, ct);
        if (entity == null) return null;
        var normalized = NormalizeName(name);
        if (await _context.ProductColors.AnyAsync(c => c.Id != id && c.Name.ToLower() == normalized.ToLower(), ct))
            throw new InvalidOperationException("Такое значение уже есть");
        entity.Name = normalized;
        await _context.SaveChangesAsync(ct);
        return new LookupItemDto { Id = entity.Id, Name = entity.Name };
    }

    public async Task<LookupItemDto?> UpdateConditionAsync(int id, string name, CancellationToken ct = default)
    {
        var entity = await _context.ProductConditions.FindAsync(new object[] { id }, ct);
        if (entity == null) return null;
        var normalized = NormalizeName(name);
        if (await _context.ProductConditions.AnyAsync(c => c.Id != id && c.Name.ToLower() == normalized.ToLower(), ct))
            throw new InvalidOperationException("Такое значение уже есть");
        entity.Name = normalized;
        await _context.SaveChangesAsync(ct);
        return new LookupItemDto { Id = entity.Id, Name = entity.Name };
    }

    public async Task<LookupItemDto?> UpdateNuanceAsync(int id, string name, CancellationToken ct = default)
    {
        var entity = await _context.ProductNuances.FindAsync(new object[] { id }, ct);
        if (entity == null) return null;
        var normalized = NormalizeName(name);
        if (await _context.ProductNuances.AnyAsync(c => c.Id != id && c.Name.ToLower() == normalized.ToLower(), ct))
            throw new InvalidOperationException("Такое значение уже есть");
        entity.Name = normalized;
        await _context.SaveChangesAsync(ct);
        return new LookupItemDto { Id = entity.Id, Name = entity.Name };
    }

    public async Task<bool> DeleteColorAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.ProductColors.FindAsync(new object[] { id }, ct);
        if (entity == null) return false;
        _context.ProductColors.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteConditionAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.ProductConditions.FindAsync(new object[] { id }, ct);
        if (entity == null) return false;
        _context.ProductConditions.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteNuanceAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.ProductNuances.FindAsync(new object[] { id }, ct);
        if (entity == null) return false;
        _context.ProductNuances.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string> EnsureNuanceExistsAsync(string name, CancellationToken ct = default)
    {
        var normalized = NormalizeName(name);
        if (string.IsNullOrEmpty(normalized))
            return string.Empty;

        var existing = await _context.ProductNuances
            .FirstOrDefaultAsync(n => n.Name.ToLower() == normalized.ToLower(), ct);
        if (existing != null)
            return existing.Name;

        _context.ProductNuances.Add(new ProductNuance { Name = normalized });
        await _context.SaveChangesAsync(ct);
        return normalized;
    }

    private static List<LookupItemDto> MapColors(List<ProductColor> items, Dictionary<string, int> counts) =>
        items.Select(i => new LookupItemDto
        {
            Id = i.Id,
            Name = i.Name,
            ProductCount = counts.GetValueOrDefault(i.Name.Trim().ToLowerInvariant(), 0)
        }).ToList();

    private static List<LookupItemDto> MapConditions(List<ProductCondition> items, Dictionary<string, int> counts) =>
        items.Select(i => new LookupItemDto
        {
            Id = i.Id,
            Name = i.Name,
            ProductCount = counts.GetValueOrDefault(i.Name.Trim().ToLowerInvariant(), 0)
        }).ToList();

    private static List<LookupItemDto> MapNuances(List<ProductNuance> items, Dictionary<string, int> counts) =>
        items.Select(i => new LookupItemDto
        {
            Id = i.Id,
            Name = i.Name,
            ProductCount = counts.GetValueOrDefault(i.Name.Trim().ToLowerInvariant(), 0)
        }).ToList();

    private async Task<Dictionary<string, int>> BuildCountMapAsync(
        Func<Product, string?> selector,
        CancellationToken ct)
    {
        var products = await _context.Products.AsNoTracking().ToListAsync(ct);
        return products
            .Select(selector)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v!.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static string NormalizeName(string name)
    {
        var n = name.Trim();
        if (string.IsNullOrEmpty(n))
            throw new InvalidOperationException("Укажите название");
        if (n.Length > 100)
            throw new InvalidOperationException("Слишком длинное название");
        return n;
    }
}

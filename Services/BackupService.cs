using System.IO.Compression;
using System.Text.Json;
using Bebochka.Api.Data;
using Bebochka.Api.Helpers;
using Bebochka.Api.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Bebochka.Api.Services;

public class BackupService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackupJobStore _jobs;
    private readonly IWebHostEnvironment _env;

    public BackupService(IServiceScopeFactory scopeFactory, BackupJobStore jobs, IWebHostEnvironment env)
    {
        _scopeFactory = scopeFactory;
        _jobs = jobs;
        _env = env;
    }

    public string StartJob(DateOnly dateFrom, DateOnly dateTo)
    {
        if (dateTo < dateFrom)
            throw new ArgumentException("Дата «по» не может быть раньше даты «с».");

        var jobId = Guid.NewGuid().ToString("N");
        _jobs.Create(jobId);
        _ = Task.Run(() => RunJobAsync(jobId, dateFrom, dateTo, CancellationToken.None));
        return jobId;
    }

    private async Task RunJobAsync(string jobId, DateOnly dateFrom, DateOnly dateTo, CancellationToken ct)
    {
        var tmpRoot = Path.Combine(Path.GetTempPath(), "bebochka-backup");
        Directory.CreateDirectory(tmpRoot);
        var stamp = $"{dateFrom:yyyy-MM-dd}_{dateTo:yyyy-MM-dd}_{DateTime.UtcNow:HH-mm-ss}";
        var zipPath = Path.Combine(tmpRoot, $"bebochka_backup_{stamp}.zip");

        try
        {
            _jobs.SetProgress(jobId, 2, "Подготовка…");

            var fromUtc = DateTimeHelper.FromMoscowTime(dateFrom.ToDateTime(TimeOnly.MinValue));
            var toUtc = DateTimeHelper.FromMoscowTime(dateTo.ToDateTime(new TimeOnly(23, 59, 59, 999)));

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var uploadsDir = Path.Combine(AppPaths.WwwRoot(_env), "uploads");
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            _jobs.SetProgress(jobId, 5, "Загрузка записей из базы…");

            var products = await db.Products.AsNoTracking()
                .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt <= toUtc)
                .ToListAsync(ct);
            _jobs.SetProgress(jobId, 10, "Товары…");

            var orders = await db.Orders.AsNoTracking()
                .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc)
                .ToListAsync(ct);
            _jobs.SetProgress(jobId, 15, "Заказы…");

            var orderItems = await db.OrderItems.AsNoTracking()
                .Where(i => i.CreatedAt >= fromUtc && i.CreatedAt <= toUtc)
                .ToListAsync(ct);
            _jobs.SetProgress(jobId, 20, "Позиции заказов…");

            var statusHistories = await db.OrderStatusHistories.AsNoTracking()
                .Where(h => h.ChangedAtUtc >= fromUtc && h.ChangedAtUtc <= toUtc)
                .ToListAsync(ct);
            _jobs.SetProgress(jobId, 24, "История статусов…");

            var reviews = await db.OrderCustomerReviews.AsNoTracking()
                .Where(r => r.CreatedAtUtc >= fromUtc && r.CreatedAtUtc <= toUtc)
                .ToListAsync(ct);
            _jobs.SetProgress(jobId, 28, "Отзывы…");

            var users = await db.Users.AsNoTracking()
                .Where(u => u.CreatedAt >= fromUtc && u.CreatedAt <= toUtc)
                .ToListAsync(ct);

            var cartItems = await db.CartItems.AsNoTracking()
                .Where(c => c.CreatedAt >= fromUtc && c.CreatedAt <= toUtc)
                .ToListAsync(ct);

            var announcements = await db.Announcements.AsNoTracking()
                .Where(a => a.CreatedAt >= fromUtc && a.CreatedAt <= toUtc)
                .ToListAsync(ct);

            var shipments = await db.IncomingShipments.AsNoTracking()
                .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt <= toUtc)
                .ToListAsync(ct);

            var miscExpenses = await db.IncomingShipmentExpenses.AsNoTracking()
                .Where(e => e.CreatedAt >= fromUtc && e.CreatedAt <= toUtc)
                .ToListAsync(ct);

            var reserveQueue = await db.ReserveQueue.AsNoTracking()
                .Where(r => r.CreatedAt >= fromUtc && r.CreatedAt <= toUtc)
                .ToListAsync(ct);

            List<TelegramError> telegramErrors;
            try
            {
                telegramErrors = await db.TelegramErrors.AsNoTracking()
                    .Where(e => e.ErrorDate >= fromUtc && e.ErrorDate <= toUtc)
                    .ToListAsync(ct);
            }
            catch (Exception ex) when (IsMissingTelegramErrorsTable(ex))
            {
                // Old production DBs may not have this table yet; backup should still finish.
                telegramErrors = new List<TelegramError>();
            }

            var consentLogs = await db.PersonalDataConsentLogs.AsNoTracking()
                .Where(l => l.AcceptedAtUtc >= fromUtc && l.AcceptedAtUtc <= toUtc)
                .ToListAsync(ct);

            _jobs.SetProgress(jobId, 32, "Сбор путей к фотографиям…");

            var imagePaths = CollectImagePaths(products, reviews, announcements);
            var filesToZip = ResolveUploadFiles(uploadsDir, imagePaths);

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            _jobs.SetProgress(jobId, 38, "Формирование архива…");

            await using (var zipFs = File.Create(zipPath))
            using (var zip = new ZipArchive(zipFs, ZipArchiveMode.Create, leaveOpen: false))
            {
                await WriteJsonEntryAsync(zip, "data/products.json", products, ct);
                _jobs.SetProgress(jobId, 42, "Архив: товары…");

                await WriteJsonEntryAsync(zip, "data/orders.json", orders, ct);
                await WriteJsonEntryAsync(zip, "data/order_items.json", orderItems, ct);
                await WriteJsonEntryAsync(zip, "data/order_status_histories.json", statusHistories, ct);
                await WriteJsonEntryAsync(zip, "data/order_customer_reviews.json", reviews, ct);
                await WriteJsonEntryAsync(zip, "data/users.json", users, ct);
                await WriteJsonEntryAsync(zip, "data/cart_items.json", cartItems, ct);
                await WriteJsonEntryAsync(zip, "data/announcements.json", announcements, ct);
                await WriteJsonEntryAsync(zip, "data/incoming_shipments.json", shipments, ct);
                await WriteJsonEntryAsync(zip, "data/incoming_shipment_expenses.json", miscExpenses, ct);
                await WriteJsonEntryAsync(zip, "data/reserve_queue.json", reserveQueue, ct);
                await WriteJsonEntryAsync(zip, "data/telegram_errors.json", telegramErrors, ct);
                await WriteJsonEntryAsync(zip, "data/personal_data_consent_logs.json", consentLogs, ct);

                _jobs.SetProgress(jobId, 48, "Манифест…");
                var manifest = new
                {
                    version = 2,
                    created_utc = DateTime.UtcNow,
                    date_from = dateFrom.ToString("yyyy-MM-dd"),
                    date_to = dateTo.ToString("yyyy-MM-dd"),
                    from_utc = fromUtc,
                    to_utc = toUtc,
                    counts = new
                    {
                        products = products.Count,
                        orders = orders.Count,
                        orderItems = orderItems.Count,
                        reviews = reviews.Count,
                        images = filesToZip.Count
                    }
                };
                await WriteJsonEntryAsync(zip, "manifest.json", manifest, ct);

                var totalImages = filesToZip.Count;
                if (totalImages == 0)
                {
                    _jobs.SetProgress(jobId, 95, "Фотографий в периоде нет");
                }
                else
                {
                    for (var i = 0; i < totalImages; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var (rel, fullPath) = filesToZip[i];
                        var entry = zip.CreateEntry($"uploads/{rel}", CompressionLevel.Optimal);
                        await using var entryStream = entry.Open();
                        await using var fs = File.OpenRead(fullPath);
                        await fs.CopyToAsync(entryStream, ct);

                        var pct = 48 + (int)((i + 1) * 47.0 / totalImages);
                        _jobs.SetProgress(jobId, pct, $"Фотографии: {i + 1} из {totalImages}");
                    }
                }
            }

            var fileName = Path.GetFileName(zipPath);
            _jobs.Complete(jobId, zipPath, fileName);
        }
        catch (Exception ex)
        {
            if (File.Exists(zipPath))
            {
                try { File.Delete(zipPath); } catch { /* ignore */ }
            }
            _jobs.Fail(jobId, ex.Message);
        }
    }

    private static async Task WriteJsonEntryAsync<T>(ZipArchive zip, string entryName, T data, CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, data, JsonOpts, ct);
    }

    private static HashSet<string> CollectImagePaths(
        List<Product> products,
        List<OrderCustomerReview> reviews,
        List<Announcement> announcements)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in products)
        {
            foreach (var img in p.Images)
                AddImagePath(paths, img);
        }
        foreach (var r in reviews)
        {
            if (string.IsNullOrWhiteSpace(r.ReviewImagesJson)) continue;
            try
            {
                var imgs = JsonSerializer.Deserialize<List<string>>(r.ReviewImagesJson) ?? new List<string>();
                foreach (var img in imgs)
                    AddImagePath(paths, img);
            }
            catch { /* skip invalid json */ }
        }
        foreach (var a in announcements)
        {
            foreach (var img in a.CollageImages)
                AddImagePath(paths, img);
        }
        return paths;
    }

    private static void AddImagePath(HashSet<string> set, string? path)
    {
        var rel = ToUploadsRelative(path);
        if (!string.IsNullOrEmpty(rel))
            set.Add(rel);
    }

    private static string? ToUploadsRelative(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var p = path.Trim().Replace('\\', '/');
        if (p.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return p["/uploads/".Length..];
        if (p.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            return p["uploads/".Length..];
        return null;
    }

    private static List<(string Rel, string FullPath)> ResolveUploadFiles(string uploadsDir, HashSet<string> relativePaths)
    {
        var list = new List<(string, string)>();
        foreach (var rel in relativePaths.OrderBy(x => x))
        {
            var full = Path.Combine(uploadsDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full))
                list.Add((rel.Replace('\\', '/'), full));
        }
        return list;
    }

    private static bool IsMissingTelegramErrorsTable(Exception ex)
    {
        if (ex is MySqlException mySqlEx && mySqlEx.Message.Contains("telegramerrors", StringComparison.OrdinalIgnoreCase))
            return true;
        return ex.Message.Contains("telegramerrors", StringComparison.OrdinalIgnoreCase);
    }
}

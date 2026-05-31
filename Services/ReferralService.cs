using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Services;

public class ReferralService : IReferralService
{
    public const decimal ReferrerRewardPercent = 10m;
    public const decimal ReferredRewardPercent = 10m;

    public static class ReferralStatus
    {
        public const string Pending = "Pending";
        public const string Registered = "Registered";
        public const string FirstOrderCompleted = "FirstOrderCompleted";
        public const string RewardGranted = "RewardGranted";
    }

    private const string RulesText =
        "Пригласи друга — получи скидку 10%. Поделитесь своим личным кодом. " +
        "И вы, и тот, кого вы пригласили, получите скидку 10% от всей суммы заказа. " +
        "Вы получаете 10% за каждого приглашённого друга. Количество друзей не ограничено. " +
        "Скидки не суммируются в одном заказе. " +
        "Приглашённым может быть только новый пользователь, у которого нет ранее совершённых заказов и который не был нашим покупателем в Telegram-канале.";

    private static readonly string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly AppDbContext _context;
    private readonly ILogger<ReferralService> _logger;

    public ReferralService(AppDbContext context, ILogger<ReferralService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MyReferralInfoDto> GetMyReferralInfoAsync(int userId, CancellationToken ct = default)
    {
        var myCode = await _context.ReferralCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        var asReferred = await _context.Referrals
            .AsNoTracking()
            .Include(r => r.ReferralCode)
            .Include(r => r.ReferrerUser)
            .FirstOrDefaultAsync(r => r.ReferredUserId == userId, ct);

        var invited = await _context.Referrals
            .AsNoTracking()
            .Include(r => r.ReferredUser)
            .Where(r => r.ReferrerUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var canApply = await CanApplyReferrerCodeAsync(userId, ct);

        return new MyReferralInfoDto
        {
            MyCode = myCode?.Code,
            CanGenerateCode = myCode == null,
            ReferredBy = asReferred == null
                ? null
                : new ReferredByInfoDto
                {
                    Code = asReferred.ReferralCode?.Code ?? string.Empty,
                    ReferrerName = asReferred.ReferrerUser?.FullName ?? asReferred.ReferrerUser?.Username,
                    Status = MapStatusLabel(asReferred.Status),
                    AppliedAt = asReferred.RegisteredAt ?? asReferred.CreatedAt
                },
            CanApplyReferrerCode = canApply,
            InvitedCount = invited.Count,
            Invited = invited.Select(r => new MyReferralInviteDto
            {
                Id = r.Id,
                ReferredName = r.ReferredUser?.FullName ?? r.ReferredUser?.Username,
                Status = MapStatusLabel(r.Status),
                CreatedAt = r.CreatedAt,
                RegisteredAt = r.RegisteredAt,
                ReferrerRewardAmount = r.ReferrerRewardAmount
            }).ToList(),
            Rules = RulesText
        };
    }

    public async Task<string> EnsureMyReferralCodeAsync(int userId, CancellationToken ct = default)
    {
        var existing = await _context.ReferralCodes.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (existing != null)
            return existing.Code;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = GenerateCode();
            var taken = await _context.ReferralCodes.AnyAsync(c => c.Code == code, ct);
            if (taken) continue;

            var entity = new ReferralCode
            {
                UserId = userId,
                Code = code,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.ReferralCodes.Add(entity);
            await _context.SaveChangesAsync(ct);
            return code;
        }

        throw new InvalidOperationException("Не удалось сгенерировать уникальный код приглашения");
    }

    public async Task ApplyReferrerCodeAsync(int userId, string code, CancellationToken ct = default)
    {
        var normalized = NormalizeCode(code);
        if (string.IsNullOrEmpty(normalized))
            throw new InvalidOperationException("Укажите код приглашения");

        if (!await CanApplyReferrerCodeAsync(userId, ct))
            throw new InvalidOperationException(
                "Код пригласившего можно указать только один раз и до первого заказа, если вы ещё не пользовались сервисом");

        var referralCode = await _context.ReferralCodes
            .FirstOrDefaultAsync(c => c.Code == normalized && c.IsActive, ct);

        if (referralCode == null)
            throw new InvalidOperationException("Код не найден или неактивен");

        if (referralCode.UserId == userId)
            throw new InvalidOperationException("Нельзя указать свой собственный код");

        var alreadyReferred = await _context.Referrals
            .AnyAsync(r => r.ReferredUserId == userId, ct);
        if (alreadyReferred)
            throw new InvalidOperationException("Код пригласившего уже указан");

        var now = DateTime.UtcNow;
        _context.Referrals.Add(new Referral
        {
            ReferrerUserId = referralCode.UserId,
            ReferredUserId = userId,
            ReferralCodeId = referralCode.Id,
            Status = ReferralStatus.Registered,
            CreatedAt = now,
            RegisteredAt = now
        });

        await _context.SaveChangesAsync(ct);
    }

    public async Task ProcessOrderReceivedAsync(int userId, int orderId, decimal orderFinalAmount, CancellationToken ct = default)
    {
        if (orderFinalAmount < 0)
            orderFinalAmount = 0;

        var asReferred = await _context.Referrals
            .FirstOrDefaultAsync(r =>
                r.ReferredUserId == userId &&
                r.Status == ReferralStatus.Registered, ct);

        if (asReferred == null)
            return;

        var receivedCount = await _context.Orders
            .CountAsync(o => o.UserId == userId && o.Status == OrderService.StatusReceived, ct);

        if (receivedCount != 1)
            return;

        asReferred.Status = ReferralStatus.RewardGranted;
        asReferred.FirstOrderId = orderId;
        asReferred.ReferredRewardAmount = Math.Round(orderFinalAmount * ReferredRewardPercent / 100m, 2);
        asReferred.ReferrerRewardAmount = Math.Round(orderFinalAmount * ReferrerRewardPercent / 100m, 2);
        asReferred.RewardGrantedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Referral reward granted for referred user {UserId}, order {OrderId}", userId, orderId);
    }

    public async Task<List<AdminReferralListItemDto>> SearchReferralsAsync(
        string? search,
        string? status,
        CancellationToken ct = default)
    {
        var query = _context.Referrals
            .AsNoTracking()
            .Include(r => r.ReferralCode)
            .Include(r => r.ReferrerUser)
            .Include(r => r.ReferredUser)
            .Include(r => r.FirstOrder)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(r => r.Status == st);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            query = query.Where(r =>
                (r.ReferralCode != null && r.ReferralCode.Code.ToLower().Contains(q)) ||
                (r.ReferrerUser != null && (
                    (r.ReferrerUser.FullName != null && r.ReferrerUser.FullName.ToLower().Contains(q)) ||
                    r.ReferrerUser.Username.ToLower().Contains(q) ||
                    (r.ReferrerUser.Phone != null && r.ReferrerUser.Phone.Contains(q)))) ||
                (r.ReferredUser != null && (
                    (r.ReferredUser.FullName != null && r.ReferredUser.FullName.ToLower().Contains(q)) ||
                    r.ReferredUser.Username.ToLower().Contains(q) ||
                    (r.ReferredUser.Phone != null && r.ReferredUser.Phone.Contains(q)))));
        }

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        return rows.Select(r => new AdminReferralListItemDto
        {
            Id = r.Id,
            Code = r.ReferralCode?.Code ?? string.Empty,
            Status = MapStatusLabel(r.Status),
            CreatedAt = r.CreatedAt,
            RegisteredAt = r.RegisteredAt,
            RewardGrantedAt = r.RewardGrantedAt,
            ReferrerUserId = r.ReferrerUserId,
            ReferrerName = r.ReferrerUser?.FullName ?? r.ReferrerUser?.Username,
            ReferrerPhone = r.ReferrerUser?.Phone,
            ReferredUserId = r.ReferredUserId,
            ReferredName = r.ReferredUser?.FullName ?? r.ReferredUser?.Username,
            ReferredPhone = r.ReferredUser?.Phone,
            FirstOrderId = r.FirstOrderId,
            FirstOrderNumber = r.FirstOrder?.OrderNumber,
            ReferrerRewardAmount = r.ReferrerRewardAmount,
            ReferredRewardAmount = r.ReferredRewardAmount
        }).ToList();
    }

    private async Task<bool> CanApplyReferrerCodeAsync(int userId, CancellationToken ct)
    {
        var hasReferrer = await _context.Referrals.AnyAsync(r => r.ReferredUserId == userId, ct);
        if (hasReferrer) return false;

        var hasOrders = await _context.Orders
            .AnyAsync(o => o.UserId == userId && o.Status != "Отменен", ct);
        return !hasOrders;
    }

    private static string GenerateCode()
    {
        var suffix = new string(Enumerable.Range(0, 6)
            .Select(_ => CodeChars[Random.Shared.Next(CodeChars.Length)])
            .ToArray());
        return $"BEBO-{suffix}";
    }

    private static string NormalizeCode(string raw) =>
        raw.Trim().ToUpperInvariant().Replace(" ", string.Empty);

    private static string MapStatusLabel(string status) => status switch
    {
        ReferralStatus.Pending => "Ожидает регистрации",
        ReferralStatus.Registered => "Зарегистрирован",
        ReferralStatus.FirstOrderCompleted => "Первый заказ выполнен",
        ReferralStatus.RewardGranted => "Скидка начислена",
        _ => status
    };
}

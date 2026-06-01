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

    public static class ReferralDiscountKind
    {
        public const string Referred = "Referred";
        public const string Referrer = "Referrer";
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
        await DbSchemaBootstrap.EnsureReferralsReadyAsync(_context, _logger, ct);

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
        var hasOrders = await UserHasNonCancelledOrdersAsync(userId, excludeOrderId: null, ct);
        var referredDiscountAvailable = !hasOrders
            && asReferred != null
            && asReferred.ReferredDiscountOrderId == null
            && asReferred.Status != ReferralStatus.Pending;

        var cartDiscountOptions = await GetCartReferralDiscountOptionsAsync(userId, ct);

        return new MyReferralInfoDto
        {
            MyCode = myCode?.Code,
            CanGenerateCode = myCode == null,
            ReferredBy = asReferred == null
                ? null
                : new ReferredByInfoDto
                {
                    ReferralId = asReferred.Id,
                    Code = asReferred.ReferralCode?.Code ?? string.Empty,
                    ReferrerName = asReferred.ReferrerUser?.FullName ?? asReferred.ReferrerUser?.Username,
                    Status = MapStatusLabel(asReferred.Status),
                    AppliedAt = asReferred.RegisteredAt ?? asReferred.CreatedAt,
                    DiscountUsed = asReferred.ReferredDiscountOrderId != null
                },
            ReferredDiscountAvailable = referredDiscountAvailable,
            HasPriorOrders = hasOrders,
            CanApplyReferrerCode = canApply,
            InvitedCount = invited.Count,
            CartDiscountOptions = cartDiscountOptions,
            Invited = invited.Select(r => new MyReferralInviteDto
            {
                Id = r.Id,
                ReferredName = r.ReferredUser?.FullName ?? r.ReferredUser?.Username,
                Status = MapStatusLabel(r.Status),
                CreatedAt = r.CreatedAt,
                RegisteredAt = r.RegisteredAt,
                ReferrerRewardAmount = r.ReferrerRewardAmount,
                ReferrerDiscountUsed = r.ReferrerDiscountOrderId != null
            }).ToList(),
            Rules = RulesText
        };
    }

    public async Task<string> EnsureMyReferralCodeAsync(int userId, CancellationToken ct = default)
    {
        await DbSchemaBootstrap.EnsureReferralsReadyAsync(_context, _logger, ct);

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
        await DbSchemaBootstrap.EnsureReferralsReadyAsync(_context, _logger, ct);

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

    public async Task<List<CartReferralDiscountOptionDto>> GetCartReferralDiscountOptionsAsync(
        int userId,
        CancellationToken ct = default)
    {
        await DbSchemaBootstrap.EnsureReferralsReadyAsync(_context, _logger, ct);

        var options = new List<CartReferralDiscountOptionDto>();
        var hasOrders = await UserHasNonCancelledOrdersAsync(userId, excludeOrderId: null, ct);

        if (!hasOrders)
        {
            var asReferred = await _context.Referrals
                .AsNoTracking()
                .Include(r => r.ReferrerUser)
                .FirstOrDefaultAsync(r =>
                    r.ReferredUserId == userId &&
                    r.ReferredDiscountOrderId == null &&
                    r.Status != ReferralStatus.Pending, ct);

            if (asReferred != null)
            {
                var referrerName = asReferred.ReferrerUser?.FullName ?? asReferred.ReferrerUser?.Username;
                options.Add(new CartReferralDiscountOptionDto
                {
                    ReferralId = asReferred.Id,
                    Kind = ReferralDiscountKind.Referred,
                    Label = "Скидка 10% — первый заказ по приглашению",
                    ForUserName = referrerName,
                    DiscountPercent = (int)ReferredRewardPercent
                });
            }
        }

        var invites = await _context.Referrals
            .AsNoTracking()
            .Include(r => r.ReferredUser)
            .Where(r =>
                r.ReferrerUserId == userId &&
                r.ReferredUserId != null &&
                r.ReferrerDiscountOrderId == null &&
                r.Status != ReferralStatus.Pending)
            .OrderByDescending(r => r.RegisteredAt ?? r.CreatedAt)
            .ToListAsync(ct);

        foreach (var inv in invites)
        {
            var name = inv.ReferredUser?.FullName ?? inv.ReferredUser?.Username ?? "приглашённый";
            options.Add(new CartReferralDiscountOptionDto
            {
                ReferralId = inv.Id,
                Kind = ReferralDiscountKind.Referrer,
                Label = $"Скидка 10% — за приглашение",
                ForUserName = name,
                DiscountPercent = (int)ReferrerRewardPercent
            });
        }

        return options;
    }

    public async Task<int?> ResolveReferralIdForCheckoutAsync(
        int userId,
        string kind,
        int? referralId,
        CancellationToken ct = default)
    {
        if (referralId is > 0)
            return referralId;

        await DbSchemaBootstrap.EnsureReferralsReadyAsync(_context, _logger, ct);

        var normalizedKind = kind?.Trim() ?? string.Empty;
        if (normalizedKind == ReferralDiscountKind.Referred)
        {
            var asReferred = await _context.Referrals
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.ReferredUserId == userId &&
                    r.ReferredDiscountOrderId == null &&
                    r.Status != ReferralStatus.Pending, ct);
            return asReferred?.Id;
        }

        return null;
    }

    public async Task ApplyReferralDiscountToOrderAsync(
        int userId,
        int orderId,
        int referralId,
        string kind,
        decimal orderTotalAmount,
        CancellationToken ct = default)
    {
        await DbSchemaBootstrap.EnsureReferralsReadyAsync(_context, _logger, ct);

        var normalizedKind = kind?.Trim() ?? string.Empty;
        if (normalizedKind != ReferralDiscountKind.Referred && normalizedKind != ReferralDiscountKind.Referrer)
            throw new InvalidOperationException("Некорректный тип реферальной скидки");

        var referral = await _context.Referrals
            .Include(r => r.ReferredUser)
            .FirstOrDefaultAsync(r => r.Id == referralId, ct);

        if (referral == null)
            throw new InvalidOperationException("Реферальная скидка не найдена");

        var order = await _context.Orders.FindAsync(new object[] { orderId }, ct);
        if (order == null)
            throw new InvalidOperationException("Заказ не найден");

        if (order.UserId != userId)
            throw new InvalidOperationException("Скидку можно применить только к своему заказу");

        var discountAmount = Math.Round(orderTotalAmount * ReferredRewardPercent / 100m, 2);

        if (normalizedKind == ReferralDiscountKind.Referred)
        {
            if (referral.ReferredUserId != userId)
                throw new InvalidOperationException("Эта скидка недоступна для вашего аккаунта");
            if (referral.ReferredDiscountOrderId != null)
                throw new InvalidOperationException("Скидка по приглашению уже была использована");
            if (referral.Status == ReferralStatus.Pending)
                throw new InvalidOperationException("Скидка по приглашению недоступна");
            if (await UserHasNonCancelledOrdersAsync(userId, orderId, ct))
                throw new InvalidOperationException("Скидка доступна только на первый заказ");

            referral.ReferredDiscountOrderId = orderId;
            referral.FirstOrderId = orderId;
            referral.ReferredRewardAmount = discountAmount;
            referral.Status = ReferralStatus.RewardGranted;
            referral.RewardGrantedAt = DateTime.UtcNow;
        }
        else
        {
            if (referral.ReferrerUserId != userId)
                throw new InvalidOperationException("Эта скидка недоступна для вашего аккаунта");
            if (referral.ReferrerDiscountOrderId != null)
                throw new InvalidOperationException("Скидка за этого приглашённого уже была использована");
            if (referral.ReferredUserId == null)
                throw new InvalidOperationException("Приглашённый ещё не зарегистрировался");

            referral.ReferrerDiscountOrderId = orderId;
            referral.ReferrerRewardAmount = discountAmount;
            if (referral.Status == ReferralStatus.Registered)
                referral.Status = ReferralStatus.RewardGranted;
            if (referral.RewardGrantedAt == null)
                referral.RewardGrantedAt = DateTime.UtcNow;
        }

        order.DiscountType = "Fixed";
        order.FixedDiscountPercent = (int)ReferredRewardPercent;
        order.Condition1ItemPercent = null;
        order.Condition3ItemsPercent = null;
        order.Condition5PlusPercent = null;
        order.ReferralId = referralId;
        order.ReferralDiscountKind = normalizedKind;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Referral discount {Kind} applied: user {UserId}, referral {ReferralId}, order {OrderId}",
            normalizedKind, userId, referralId, orderId);
    }

    public async Task ProcessOrderReceivedAsync(int userId, int orderId, decimal orderFinalAmount, CancellationToken ct = default)
    {
        await DbSchemaBootstrap.EnsureReferralsReadyAsync(_context, _logger, ct);

        if (orderFinalAmount < 0)
            orderFinalAmount = 0;

        var asReferred = await _context.Referrals
            .FirstOrDefaultAsync(r => r.ReferredUserId == userId, ct);

        if (asReferred == null)
            return;

        if (asReferred.ReferredDiscountOrderId != null)
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
        _logger.LogInformation("Referral reward recorded on receive for referred user {UserId}, order {OrderId}", userId, orderId);
    }

    private async Task<bool> UserHasNonCancelledOrdersAsync(int userId, int? excludeOrderId = null, CancellationToken ct = default)
    {
        return await _context.Orders
            .AnyAsync(o =>
                o.UserId == userId &&
                o.Status != "Отменен" &&
                (excludeOrderId == null || o.Id != excludeOrderId), ct);
    }

    public async Task<List<AdminReferralListItemDto>> SearchReferralsAsync(
        string? search,
        string? status,
        CancellationToken ct = default)
    {
        await DbSchemaBootstrap.EnsureReferralsReadyAsync(_context, _logger, ct);

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

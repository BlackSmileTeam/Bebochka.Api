using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Services;

public interface IReferralService
{
    Task<MyReferralInfoDto> GetMyReferralInfoAsync(int userId, CancellationToken ct = default);
    Task<string> EnsureMyReferralCodeAsync(int userId, CancellationToken ct = default);
    Task ApplyReferrerCodeAsync(int userId, string code, CancellationToken ct = default);
    Task<List<CartReferralDiscountOptionDto>> GetCartReferralDiscountOptionsAsync(int userId, CancellationToken ct = default);
    Task<int?> ResolveReferralIdForCheckoutAsync(int userId, string kind, int? referralId, CancellationToken ct = default);
    Task ApplyReferralDiscountToOrderAsync(int userId, int orderId, int referralId, string kind, decimal orderTotalAmount, CancellationToken ct = default);
    Task ProcessOrderReceivedAsync(int userId, int orderId, decimal orderFinalAmount, CancellationToken ct = default);
    Task<List<AdminReferralListItemDto>> SearchReferralsAsync(string? search, string? status, CancellationToken ct = default);
}

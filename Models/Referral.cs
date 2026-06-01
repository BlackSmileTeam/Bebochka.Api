namespace Bebochka.Api.Models;

/// <summary>
/// Tracks a referral invite: who invited whom and reward lifecycle.
/// </summary>
public class Referral
{
    public int Id { get; set; }

    public int ReferrerUserId { get; set; }

    /// <summary>Filled when the invited person registers.</summary>
    public int? ReferredUserId { get; set; }

    public int ReferralCodeId { get; set; }

    /// <summary>Pending, Registered, FirstOrderCompleted, RewardGranted.</summary>
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RegisteredAt { get; set; }

    public int? FirstOrderId { get; set; }

    /// <summary>Order where the invited user applied their 10% first-order discount.</summary>
    public int? ReferredDiscountOrderId { get; set; }

    /// <summary>Order where the referrer applied their 10% reward for this invite.</summary>
    public int? ReferrerDiscountOrderId { get; set; }

    public DateTime? RewardGrantedAt { get; set; }

    public decimal? ReferrerRewardAmount { get; set; }

    public decimal? ReferredRewardAmount { get; set; }

    public User? ReferrerUser { get; set; }

    public User? ReferredUser { get; set; }

    public ReferralCode? ReferralCode { get; set; }

    public Order? FirstOrder { get; set; }
}

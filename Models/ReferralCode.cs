namespace Bebochka.Api.Models;

/// <summary>
/// Unique referral code assigned to a user for the referral program.
/// </summary>
public class ReferralCode
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Public code shared with friends, e.g. BEBO-ABC123.</summary>
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}

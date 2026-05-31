namespace Bebochka.Api.Models.DTOs;

public class MyReferralInfoDto
{
    public string? MyCode { get; set; }
    public bool CanGenerateCode { get; set; }
    public ReferredByInfoDto? ReferredBy { get; set; }
    public bool CanApplyReferrerCode { get; set; }
    public int InvitedCount { get; set; }
    public List<MyReferralInviteDto> Invited { get; set; } = new();
    public string Rules { get; set; } = string.Empty;
}

public class ReferredByInfoDto
{
    public string Code { get; set; } = string.Empty;
    public string? ReferrerName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? AppliedAt { get; set; }
}

public class MyReferralInviteDto
{
    public int Id { get; set; }
    public string? ReferredName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? RegisteredAt { get; set; }
    public decimal? ReferrerRewardAmount { get; set; }
}

public class ApplyReferrerCodeDto
{
    public string Code { get; set; } = string.Empty;
}

public class AdminReferralListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? RegisteredAt { get; set; }
    public DateTime? RewardGrantedAt { get; set; }
    public int ReferrerUserId { get; set; }
    public string? ReferrerName { get; set; }
    public string? ReferrerPhone { get; set; }
    public int? ReferredUserId { get; set; }
    public string? ReferredName { get; set; }
    public string? ReferredPhone { get; set; }
    public int? FirstOrderId { get; set; }
    public string? FirstOrderNumber { get; set; }
    public decimal? ReferrerRewardAmount { get; set; }
    public decimal? ReferredRewardAmount { get; set; }
}

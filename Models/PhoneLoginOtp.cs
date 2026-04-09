namespace Bebochka.Api.Models;

/// <summary>
/// Одноразовый код входа по телефону (хранится до истечения срока).
/// </summary>
public class PhoneLoginOtp
{
    public int Id { get; set; }
    public string PhoneE164 { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

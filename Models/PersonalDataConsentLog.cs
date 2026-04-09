namespace Bebochka.Api.Models;

/// <summary>
/// Запись о принятии согласия на обработку персональных данных (аудит для спорных ситуаций).
/// </summary>
public class PersonalDataConsentLog
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Вид согласия, версия формулировки (например PersonalDataProcessing_v1).</summary>
    public string ConsentKind { get; set; } = string.Empty;

    public DateTime AcceptedAtUtc { get; set; }

    /// <summary>IPv4/IPv6 клиента (с учётом прокси при наличии заголовков).</summary>
    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>Грубая классификация: Mobile, Tablet, Desktop, Unknown.</summary>
    public string? DeviceType { get; set; }

    /// <summary>Доп. контекст JSON: Accept-Language, цепочка X-Forwarded-For и т.д.</summary>
    public string? ExtraJson { get; set; }

    public User? User { get; set; }
}

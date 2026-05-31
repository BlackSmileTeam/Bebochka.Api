namespace Bebochka.Api.Models.DTOs;

using System.Text.Json.Serialization;

public class UserChildDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string ClothingSize { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpsertUserChildDto
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Date only, yyyy-MM-dd.</summary>
    public string DateOfBirth { get; set; } = string.Empty;
    public string ClothingSize { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
}

public class UpdateMyProfileDto
{
    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }
    [JsonPropertyName("autoFilterByChildren")]
    public bool? AutoFilterByChildren { get; set; }
    /// <summary>yyyy-MM-dd or null to clear</summary>
    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }
}

public class MyProfileDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool HasVkLogin { get; set; }
    [JsonPropertyName("autoFilterByChildren")]
    public bool AutoFilterByChildren { get; set; }
    [JsonPropertyName("dateOfBirth")]
    public DateTime? DateOfBirth { get; set; }
}

public class ChangeMyPasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

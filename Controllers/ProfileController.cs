using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Current user profile and children (shop customers).
/// </summary>
[ApiController]
[Route("api/profile")]
[Authorize]
[Produces("application/json")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(AppDbContext context, ILogger<ProfileController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(MyProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MyProfileDto>> GetMyProfile()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();
        return Ok(MapProfile(user));
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(MyProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MyProfileDto>> UpdateMyProfile([FromBody] UpdateMyProfileDto dto)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (dto.FullName != null)
        {
            var name = dto.FullName.Trim();
            if (name.Length > 100) return BadRequest(new { message = "Имя слишком длинное" });
            user.FullName = string.IsNullOrEmpty(name) ? null : name;
        }

        if (dto.Email != null)
        {
            var email = dto.Email.Trim();
            if (email.Length > 100) return BadRequest(new { message = "Email слишком длинный" });
            if (!string.IsNullOrEmpty(email) && !email.Contains('@'))
                return BadRequest(new { message = "Некорректный email" });
            user.Email = string.IsNullOrEmpty(email) ? null : email;
        }

        if (dto.Phone != null)
        {
            var phone = NormalizePhoneRu(dto.Phone);
            if (phone == null && !string.IsNullOrWhiteSpace(dto.Phone))
                return BadRequest(new { message = "Укажите телефон в формате +7XXXXXXXXXX" });
            user.Phone = phone;
        }

        await _context.SaveChangesAsync();
        return Ok(MapProfile(user));
    }

    [HttpPut("me/password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ChangeMyPassword([FromBody] ChangeMyPasswordDto dto)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (user.VkUserId != null)
            return BadRequest(new { message = "Смена пароля недоступна для входа через VK" });

        if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
            return BadRequest(new { message = "Укажите текущий пароль" });

        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            return BadRequest(new { message = "Новый пароль должен быть не короче 6 символов" });

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Неверный текущий пароль" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Пароль изменён" });
    }

    [HttpGet("children")]
    [ProducesResponseType(typeof(List<UserChildDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserChildDto>>> GetMyChildren()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var list = await _context.UserChildren
            .AsNoTracking()
            .Where(c => c.UserId == userId.Value)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return Ok(list.Select(MapChild).ToList());
    }

    [HttpPost("children")]
    [ProducesResponseType(typeof(UserChildDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserChildDto>> CreateChild([FromBody] UpsertUserChildDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var err = ValidateChildDto(dto);
        if (err != null) return BadRequest(new { message = err });

        var now = DateTime.UtcNow;
        var child = new UserChild
        {
            UserId = userId.Value,
            Name = dto.Name.Trim(),
            DateOfBirth = dto.DateOfBirth.Date,
            ClothingSize = NormalizeClothingSize(dto.ClothingSize),
            Gender = dto.Gender.Trim().ToLowerInvariant(),
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            _context.UserChildren.Add(child);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateChild failed for user {UserId}", userId.Value);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Не удалось сохранить данные ребёнка. Попробуйте позже или обратитесь в поддержку." });
        }

        return CreatedAtAction(nameof(GetMyChildren), MapChild(child));
    }

    [HttpPut("children/{id:int}")]
    [ProducesResponseType(typeof(UserChildDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserChildDto>> UpdateChild(int id, [FromBody] UpsertUserChildDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var child = await _context.UserChildren.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId.Value);
        if (child == null) return NotFound();

        var err = ValidateChildDto(dto);
        if (err != null) return BadRequest(new { message = err });

        child.Name = dto.Name.Trim();
        child.DateOfBirth = dto.DateOfBirth.Date;
        child.ClothingSize = NormalizeClothingSize(dto.ClothingSize);
        child.Gender = dto.Gender.Trim().ToLowerInvariant();
        child.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateChild failed for user {UserId}, child {ChildId}", userId.Value, id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Не удалось сохранить данные ребёнка. Попробуйте позже или обратитесь в поддержку." });
        }

        return Ok(MapChild(child));
    }

    [HttpDelete("children/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteChild(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var child = await _context.UserChildren.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId.Value);
        if (child == null) return NotFound();

        _context.UserChildren.Remove(child);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("UserId")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return null;
        return await _context.Users.FindAsync(userId.Value);
    }

    private static MyProfileDto MapProfile(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        FullName = user.FullName,
        Email = user.Email,
        Phone = user.Phone,
        HasVkLogin = user.VkUserId != null
    };

    private static UserChildDto MapChild(UserChild c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        DateOfBirth = c.DateOfBirth,
        ClothingSize = c.ClothingSize,
        Gender = c.Gender,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };

    private static string? ValidateChildDto(UpsertUserChildDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return "Укажите имя ребёнка";
        if (dto.Name.Trim().Length > 100) return "Имя слишком длинное";
        if (string.IsNullOrWhiteSpace(dto.ClothingSize)) return "Укажите размер одежды";
        var normalizedSize = NormalizeClothingSize(dto.ClothingSize);
        if (string.IsNullOrEmpty(normalizedSize)) return "Укажите размер одежды";
        if (normalizedSize.Length > 100) return "Слишком много размеров";
        var gender = dto.Gender?.Trim().ToLowerInvariant() ?? "";
        if (gender is not ("мальчик" or "девочка")) return "Укажите пол: мальчик или девочка";
        if (dto.DateOfBirth.Date > DateTime.UtcNow.Date) return "Дата рождения не может быть в будущем";
        return null;
    }

    private static string NormalizeClothingSize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var part in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (seen.Add(part)) list.Add(part);
        }
        return string.Join(",", list);
    }

    private static string? NormalizePhoneRu(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = Regex.Replace(raw, @"\D", "");
        if (digits.StartsWith('8') && digits.Length >= 11) digits = "7" + digits[1..];
        if (digits.Length == 10) digits = "7" + digits;
        if (digits.Length != 11 || !digits.StartsWith('7')) return null;
        return "+" + digits;
    }
}

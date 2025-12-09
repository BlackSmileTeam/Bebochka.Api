using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Controller for managing admin users (only for authenticated admins)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the UsersController class
    /// </summary>
    /// <param name="context">Database context</param>
    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new admin user
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="password">Password</param>
    /// <param name="email">Email (optional)</param>
    /// <param name="fullName">Full name (optional)</param>
    /// <returns>Created user</returns>
    /// <response code="201">User created successfully</response>
    /// <response code="400">Invalid input or username already exists</response>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> CreateUser(
        [FromForm] string username,
        [FromForm] string password,
        [FromForm] string? email = null,
        [FromForm] string? fullName = null)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return BadRequest(new { message = "Username and password are required" });

        if (await _context.Users.AnyAsync(u => u.Username == username))
            return BadRequest(new { message = "Username already exists" });

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Email = email,
            FullName = fullName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            CreatedAt = user.CreatedAt
        });
    }

    /// <summary>
    /// Gets all users
    /// </summary>
    /// <returns>List of users</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        var users = await _context.Users
            .OrderBy(u => u.Username)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                FullName = u.FullName,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Checks if a user is admin by Telegram User ID
    /// </summary>
    /// <param name="telegramUserId">Telegram User ID</param>
    /// <returns>True if user is admin, false otherwise</returns>
    /// <response code="200">Returns admin status</response>
    /// <response code="404">User not found</response>
    [HttpGet("isadmin/{telegramUserId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<bool>> IsAdmin(long telegramUserId)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId);

            // Если пользователь не найден, возвращаем false (не админ)
            if (user == null)
                return Ok(false);

            return Ok(user.IsAdmin);
        }
        catch (Exception ex)
        {
            // Логируем ошибку и возвращаем false
            return Ok(false);
        }
    }

    /// <summary>
    /// Gets a user by ID
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>User information</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            CreatedAt = user.CreatedAt
        });
    }

    /// <summary>
    /// Links a Telegram User ID to an existing user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="telegramUserId">Telegram User ID</param>
    /// <returns>Success response</returns>
    /// <response code="200">Telegram User ID linked successfully</response>
    /// <response code="400">Telegram User ID already linked to another user</response>
    /// <response code="404">User not found</response>
    [HttpPut("{id}/telegram")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> LinkTelegramUserId(int id, [FromBody] LinkTelegramUserIdDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        // Проверяем, не привязан ли уже этот Telegram User ID к другому пользователю
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramUserId == dto.TelegramUserId && u.Id != id);

        if (existingUser != null)
        {
            return BadRequest(new { message = $"Telegram User ID {dto.TelegramUserId} is already linked to user {existingUser.Username}" });
        }

        user.TelegramUserId = dto.TelegramUserId;
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Telegram User ID {dto.TelegramUserId} linked to user {user.Username} successfully" });
    }

    /// <summary>
    /// Changes a user's password
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="dto">Password change data</param>
    /// <returns>Success response</returns>
    [HttpPut("{id}/password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ChangePassword(int id, [FromBody] ChangePasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            return BadRequest(new { message = "Password must be at least 6 characters long" });

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password changed successfully" });
    }

    /// <summary>
    /// Deletes a user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>Success response</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "User deleted successfully" });
    }
}


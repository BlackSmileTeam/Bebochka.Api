using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bebochka.Api.Exceptions;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Services;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Controller for authentication operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto loginDto)
    {
        var result = await _authService.LoginAsync(loginDto);
        if (result == null)
            return Unauthorized(new { message = "Invalid username or password" });
        return Ok(result);
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    {
        if (!dto.AcceptPersonalDataProcessing)
            return BadRequest(new { message = "Необходимо принять пользовательское соглашение и согласие на обработку персональных данных." });

        var result = await _authService.RegisterAsync(dto);
        if (result == null)
            return BadRequest(new { message = "Не удалось зарегистрировать: проверьте телефон, пароль (от 6 символов) и что такой номер ещё не занят." });
        return Ok(result);
    }

    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Google([FromBody] GoogleLoginDto dto)
    {
        try
        {
            var result = await _authService.LoginWithGoogleAsync(dto);
            if (result == null)
                return Unauthorized(new { message = "Google-вход недоступен (проверьте ClientId и токен)" });
            return Ok(result);
        }
        catch (ConsentRequiredException)
        {
            return BadRequest(new { message = "Для регистрации через Google необходимо принять пользовательское соглашение и согласие на обработку персональных данных." });
        }
    }

    [HttpPost("phone/send-code")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [AllowAnonymous]
    public async Task<ActionResult> SendPhoneCode([FromBody] PhoneSendCodeDto dto)
    {
        var ok = await _authService.SendPhoneLoginCodeAsync(dto);
        if (!ok)
            return BadRequest(new { message = "Некорректный номер телефона" });
        return Ok(new { message = "Код отправлен (в режиме разработки смотрите лог сервера)" });
    }

    [HttpPost("phone/verify")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> VerifyPhone([FromBody] PhoneVerifyDto dto)
    {
        try
        {
            var result = await _authService.VerifyPhoneLoginAsync(dto);
            if (result == null)
                return Unauthorized(new { message = "Неверный код или срок действия истёк" });
            return Ok(result);
        }
        catch (ConsentRequiredException)
        {
            return BadRequest(new { message = "Для регистрации по номеру телефона необходимо принять пользовательское соглашение и согласие на обработку персональных данных." });
        }
    }

    [HttpPost("merge-cart")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MergeCart([FromBody] MergeCartDto dto)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        await _authService.MergeGuestCartAsync(userId, dto.SessionId);
        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(token))
            return Unauthorized();
        var user = await _authService.ValidateTokenAsync(token);
        if (user == null)
            return Unauthorized();
        return Ok(user);
    }
}

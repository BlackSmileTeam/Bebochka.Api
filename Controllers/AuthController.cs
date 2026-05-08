using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
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
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    private static string SafeReturnPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "/";
        var p = raw.Trim();
        if (!p.StartsWith('/') || p.StartsWith("//", StringComparison.Ordinal)) return "/";
        return p.Length > 2000 ? "/" : p;
    }

    private string ResolveFrontendBaseUrl()
    {
        var configured = _configuration["App:FrontendPublicUrl"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');

        var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var scheme = !string.IsNullOrWhiteSpace(forwardedProto) ? forwardedProto : Request.Scheme;
        var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(host))
            host = Request.Host.Value;

        if (!string.IsNullOrWhiteSpace(host))
            return $"{scheme}://{host}".TrimEnd('/');

        return "https://bebochka.ru";
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

    /// <summary>
    /// Начало входа через VK: редирект на oauth.vk.com (нужны Vk:AppId, Vk:SecureKey, Vk:RedirectUri, App:FrontendPublicUrl).
    /// </summary>
    [HttpGet("vk/start")]
    [AllowAnonymous]
    public IActionResult VkStart([FromQuery] string? returnUrl, [FromQuery] string? sessionId, [FromQuery] bool acceptPersonalDataProcessing = false)
    {
        var appId = _configuration["Vk:AppId"];
        var redirectUri = _configuration["Vk:RedirectUri"];
        var frontend = ResolveFrontendBaseUrl();
        var safeReturn = SafeReturnPath(returnUrl);

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(redirectUri))
            return Redirect($"{frontend.TrimEnd('/')}/account?returnUrl={Uri.EscapeDataString(safeReturn)}&vkError=config");

        var stateObj = new VkOAuthState
        {
            ReturnUrl = safeReturn,
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim(),
            AcceptPersonalDataProcessing = acceptPersonalDataProcessing
        };
        var json = JsonSerializer.Serialize(stateObj);
        var stateB64 = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(json));

        var vkUrl =
            "https://oauth.vk.com/authorize?client_id=" + Uri.EscapeDataString(appId)
            + "&display=page&redirect_uri=" + Uri.EscapeDataString(redirectUri)
            + "&scope=" + Uri.EscapeDataString("email")
            + "&response_type=code&v=5.131&state=" + Uri.EscapeDataString(stateB64);

        return Redirect(vkUrl);
    }

    /// <summary>
    /// Callback VK OAuth: обмен code, выпуск JWT, редирект на фронт с данными во фрагменте #bebochkaAuth=...
    /// </summary>
    [HttpGet("vk/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> VkCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var frontend = ResolveFrontendBaseUrl();

        if (!string.IsNullOrEmpty(error))
            return Redirect($"{frontend.TrimEnd('/')}/account?vkError=denied");

        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(code))
            return Redirect($"{frontend.TrimEnd('/')}/account?vkError=incomplete");

        VkOAuthState stateObj;
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(state);
            stateObj = JsonSerializer.Deserialize<VkOAuthState>(bytes) ?? new VkOAuthState();
        }
        catch
        {
            return Redirect($"{frontend.TrimEnd('/')}/account?vkError=state");
        }

        var safeReturn = SafeReturnPath(stateObj.ReturnUrl);
        var (auth, err) = await _authService.CompleteVkOAuthAsync(code, stateObj, cancellationToken);
        if (auth == null)
        {
            var errCode = err switch
            {
                "consent" => "consent",
                "email_conflict" => "email_conflict",
                "config" => "config",
                _ => "failed"
            };
            return Redirect($"{frontend.TrimEnd('/')}/account?returnUrl={Uri.EscapeDataString(safeReturn)}&vkError={errCode}");
        }

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var fragmentPayload = new
        {
            auth.Token,
            auth.ExpiresAt,
            auth.Username,
            auth.FullName,
            auth.UserId,
            auth.IsAdmin,
            auth.Email
        };
        var fragmentJson = JsonSerializer.Serialize(fragmentPayload, options);
        var fragB64 = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(fragmentJson));

        var url = $"{frontend.TrimEnd('/')}/account?returnUrl={Uri.EscapeDataString(safeReturn)}#bebochkaAuth={fragB64}";
        return Redirect(url);
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

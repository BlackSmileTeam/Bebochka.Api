using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Bebochka.Api.Data;
using Bebochka.Api.Exceptions;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Utilities;
using Microsoft.AspNetCore.Http;

namespace Bebochka.Api.Services;

/// <summary>
/// Service implementation for authentication operations
/// </summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;

    public const string ConsentShopPhoneRegistration = "PersonalDataProcessing_ShopPhoneRegistration_v1";
    public const string ConsentGoogleRegistration = "PersonalDataProcessing_GoogleRegistration_v1";
    public const string ConsentPhoneRegistration = "PersonalDataProcessing_PhoneRegistration_v1";
    public const string ConsentVkRegistration = "PersonalDataProcessing_VkRegistration_v1";

    public AuthService(AppDbContext context, IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
    {
        var key = loginDto.Username?.Trim() ?? "";
        if (string.IsNullOrEmpty(key))
            return null;

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == key && u.IsActive);

        if (user == null)
        {
            var phone = PhoneAuthHelper.NormalizeRuToE164(key);
            if (phone != null)
            {
                user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Phone == phone && u.IsActive);
            }
        }

        if (user == null)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            return null;

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return BuildAuthResponse(user);
    }

    public async Task<UserDto?> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured"));

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var username = jwtToken.Claims.First(x => x.Type == ClaimTypes.Name).Value;

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

            if (user == null)
                return null;

            return MapUserDto(user);
        }
        catch
        {
            return null;
        }
    }

    public async Task<AuthResponseDto?> RegisterAsync(RegisterDto dto)
    {
        if (!dto.AcceptPersonalDataProcessing)
            return null;

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return null;

        var phoneE164 = PhoneAuthHelper.NormalizeRuToE164(dto.Phone);
        if (phoneE164 == null)
            return null;

        if (await _context.Users.AnyAsync(u => u.Phone == phoneE164))
            return null;
        if (!string.IsNullOrWhiteSpace(dto.Email) &&
            await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == dto.Email.ToLower()))
            return null;

        var username = await MakeUniqueUsernameAsync("u_" + phoneE164.TrimStart('+'));

        var user = new User
        {
            Username = username,
            Phone = phoneE164,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            FullName = string.IsNullOrWhiteSpace(dto.FullName) ? null : dto.FullName.Trim(),
            IsActive = true,
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await LogPersonalDataConsentAsync(user.Id, ConsentShopPhoneRegistration);

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponseDto?> LoginWithGoogleAsync(GoogleLoginDto dto)
    {
        var clientId = _configuration["Google:ClientId"];
        if (string.IsNullOrEmpty(clientId))
            return null;

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });
        }
        catch
        {
            return null;
        }

        var sub = payload.Subject;
        if (string.IsNullOrEmpty(sub))
            return null;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.GoogleSub == sub);
        if (user == null)
        {
            if (!dto.AcceptPersonalDataProcessing)
                throw new ConsentRequiredException();

            var email = payload.Email ?? "";
            if (!string.IsNullOrEmpty(email) && await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower()))
                return null;

            var baseName = !string.IsNullOrEmpty(payload.Name)
                ? payload.Name
                : (!string.IsNullOrEmpty(email) ? email.Split('@')[0] : "user");
            var username = await MakeUniqueUsernameAsync("g_" + SanitizeUsername(baseName));

            user = new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                Email = string.IsNullOrEmpty(email) ? null : email,
                FullName = payload.Name,
                GoogleSub = sub,
                IsActive = true,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await LogPersonalDataConsentAsync(user.Id, ConsentGoogleRegistration);
        }
        else
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return BuildAuthResponse(user);
    }

    public async Task<(AuthResponseDto? Response, string? ErrorCode)> CompleteVkOAuthAsync(string code, VkOAuthState state, CancellationToken cancellationToken = default)
    {
        var appId = _configuration["Vk:AppId"];
        var secureKey = _configuration["Vk:SecureKey"];
        var redirectUri = _configuration["Vk:RedirectUri"];
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(secureKey) || string.IsNullOrEmpty(redirectUri))
            return (null, "config");

        if (string.IsNullOrWhiteSpace(code))
            return (null, "token_exchange");

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var tokenUrl =
            "https://oauth.vk.com/access_token?client_id=" + Uri.EscapeDataString(appId)
            + "&client_secret=" + Uri.EscapeDataString(secureKey)
            + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
            + "&code=" + Uri.EscapeDataString(code.Trim());

        using var tokenResp = await client.GetAsync(tokenUrl, cancellationToken);
        var tokenJson = await tokenResp.Content.ReadAsStringAsync(cancellationToken);

        using var tokenDoc = JsonDocument.Parse(tokenJson);
        var root = tokenDoc.RootElement;
        if (root.TryGetProperty("error", out _))
            return (null, "token_exchange");

        if (!root.TryGetProperty("user_id", out var userIdEl) || !root.TryGetProperty("access_token", out var accessEl))
            return (null, "token_exchange");

        long vkUserId;
        try
        {
            vkUserId = userIdEl.ValueKind == JsonValueKind.String
                ? long.Parse(userIdEl.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture)
                : userIdEl.GetInt64();
        }
        catch
        {
            return (null, "token_exchange");
        }

        var accessToken = accessEl.GetString() ?? "";
        var email = root.TryGetProperty("email", out var emEl) && emEl.ValueKind == JsonValueKind.String
            ? emEl.GetString()
            : null;

        var infoUrl =
            "https://api.vk.com/method/users.get?user_ids=" + vkUserId
            + "&fields=first_name,last_name,nickname&v=5.131&lang=0&access_token="
            + Uri.EscapeDataString(accessToken);

        using var infoResp = await client.GetAsync(infoUrl, cancellationToken);
        var infoJson = await infoResp.Content.ReadAsStringAsync(cancellationToken);
        using var infoDoc = JsonDocument.Parse(infoJson);
        var infoRoot = infoDoc.RootElement;
        if (infoRoot.TryGetProperty("error", out _))
            return (null, "user_info");

        string? fullName = null;
        if (infoRoot.TryGetProperty("response", out var responseArr) && responseArr.ValueKind == JsonValueKind.Array && responseArr.GetArrayLength() > 0)
        {
            var u = responseArr[0];
            var fn = u.TryGetProperty("first_name", out var f) ? f.GetString() : "";
            var ln = u.TryGetProperty("last_name", out var l) ? l.GetString() : "";
            fullName = string.Join(' ', new[] { fn, ln }.Where(s => !string.IsNullOrEmpty(s)));
            if (string.IsNullOrWhiteSpace(fullName))
                fullName = null;
        }

        var user = await _context.Users.FirstOrDefaultAsync(x => x.VkUserId == vkUserId, cancellationToken);
        if (user == null)
        {
            if (!state.AcceptPersonalDataProcessing)
                return (null, "consent");

            if (!string.IsNullOrEmpty(email))
            {
                var emailLower = email.Trim().ToLowerInvariant();
                if (await _context.Users.AnyAsync(x => x.Email != null && x.Email.ToLower() == emailLower, cancellationToken))
                    return (null, "email_conflict");
            }

            var username = await MakeUniqueUsernameAsync("vk_" + vkUserId);
            user = new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                Email = string.IsNullOrEmpty(email) ? null : email.Trim(),
                FullName = fullName,
                VkUserId = vkUserId,
                IsActive = true,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            await LogPersonalDataConsentAsync(user.Id, ConsentVkRegistration);
        }
        else
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (!string.IsNullOrEmpty(state.SessionId))
            await MergeGuestCartAsync(user.Id, state.SessionId);

        return (BuildAuthResponse(user), null);
    }

    public async Task<bool> SendPhoneLoginCodeAsync(PhoneSendCodeDto dto)
    {
        var phone = PhoneAuthHelper.NormalizeRuToE164(dto.Phone);
        if (phone == null)
            return false;

        var random = new Random();
        var code = random.Next(100000, 999999).ToString();
        var expire = DateTime.UtcNow.AddMinutes(10);

        var old = await _context.PhoneLoginOtps.Where(o => o.PhoneE164 == phone).ToListAsync();
        _context.PhoneLoginOtps.RemoveRange(old);
        _context.PhoneLoginOtps.Add(new PhoneLoginOtp
        {
            PhoneE164 = phone,
            Code = code,
            ExpiresAtUtc = expire,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // В продакшене: отправка SMS. Для отладки код в логах.
        Console.WriteLine($"[Phone OTP] {phone} code: {code}");
        return true;
    }

    public async Task<AuthResponseDto?> VerifyPhoneLoginAsync(PhoneVerifyDto dto)
    {
        var phone = PhoneAuthHelper.NormalizeRuToE164(dto.Phone);
        if (phone == null || string.IsNullOrWhiteSpace(dto.Code))
            return null;

        var row = await _context.PhoneLoginOtps
            .Where(o => o.PhoneE164 == phone && o.Code == dto.Code.Trim() && o.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync();
        if (row == null)
            return null;

        _context.PhoneLoginOtps.Remove(row);
        await _context.SaveChangesAsync();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Phone == phone);
        if (user == null)
        {
            if (!dto.AcceptPersonalDataProcessing)
                throw new ConsentRequiredException();

            var username = await MakeUniqueUsernameAsync("p_" + phone.TrimStart('+'));
            user = new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                Phone = phone,
                IsActive = true,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await LogPersonalDataConsentAsync(user.Id, ConsentPhoneRegistration);
        }
        else
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return BuildAuthResponse(user);
    }

    public async Task MergeGuestCartAsync(int userId, string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;

        var guestItems = await _context.CartItems
            .Where(c => c.SessionId == sessionId && c.UserId == null)
            .ToListAsync();

        var userKey = $"uid:{userId}";
        foreach (var g in guestItems)
        {
            var existing = await _context.CartItems
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == g.ProductId);
            if (existing != null)
            {
                existing.Quantity += g.Quantity;
                existing.UpdatedAt = DateTime.UtcNow;
                _context.CartItems.Remove(g);
            }
            else
            {
                g.UserId = userId;
                g.SessionId = userKey;
                g.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task LogPersonalDataConsentAsync(int userId, string consentKind)
    {
        var http = _httpContextAccessor.HttpContext;
        var req = http?.Request;
        var ua = req?.Headers.UserAgent.ToString();
        if (ua?.Length > 8000)
            ua = ua[..8000];

        var log = new PersonalDataConsentLog
        {
            UserId = userId,
            ConsentKind = consentKind,
            AcceptedAtUtc = DateTime.UtcNow,
            IpAddress = ClientInfoHelper.GetClientIpAddress(http),
            UserAgent = string.IsNullOrEmpty(ua) ? null : ua,
            DeviceType = ClientInfoHelper.ClassifyDevice(ua),
            ExtraJson = ClientInfoHelper.BuildExtraJson(req)
        };

        _context.PersonalDataConsentLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    private async Task<string> MakeUniqueUsernameAsync(string baseName)
    {
        var name = baseName;
        var n = 0;
        while (await _context.Users.AnyAsync(u => u.Username == name))
        {
            n++;
            name = baseName + "_" + n;
        }
        return name;
    }

    private static string SanitizeUsername(string s)
    {
        var chars = s.Where(c => char.IsLetterOrDigit(c) || c == '_').Take(40).ToArray();
        return chars.Length > 0 ? new string(chars) : "user";
    }

    private AuthResponseDto BuildAuthResponse(User user)
    {
        var token = GenerateJwtToken(user);
        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Username = user.Username,
            FullName = user.FullName,
            UserId = user.Id,
            IsAdmin = user.IsAdmin,
            Email = user.Email
        };
    }

    private static UserDto MapUserDto(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        Phone = user.Phone,
        FullName = user.FullName,
        CreatedAt = user.CreatedAt,
        ChannelCustomEmojiId = user.ChannelCustomEmojiId,
        IsAdmin = user.IsAdmin
    };

    private string GenerateJwtToken(User user)
    {
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured"));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

        var role = user.IsAdmin ? "Admin" : "Customer";
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("UserId", user.Id.ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

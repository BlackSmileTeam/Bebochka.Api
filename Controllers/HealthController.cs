using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bebochka.Api.Data;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Публичная проверка доступности API (Postman, мониторинг). Без авторизации.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>GET /api/health — жив ли API и есть ли связь с БД.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var utc = DateTime.UtcNow;
        try
        {
            var dbOk = await _db.Database.CanConnectAsync(cancellationToken);
            return Ok(new
            {
                ok = dbOk,
                service = "bebochka-api",
                utc,
                database = dbOk ? "up" : "down",
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                ok = false,
                service = "bebochka-api",
                utc,
                database = "error",
                message = ex.Message,
            });
        }
    }
}

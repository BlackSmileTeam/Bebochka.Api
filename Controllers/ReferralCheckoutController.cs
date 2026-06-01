using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Services;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Referral discounts at checkout (alias for GET /api/profile/referral/cart-discounts).
/// </summary>
[ApiController]
[Route("api/referral")]
[Authorize]
[Produces("application/json")]
public class ReferralCheckoutController : ControllerBase
{
    private readonly IReferralService _referralService;

    public ReferralCheckoutController(IReferralService referralService)
    {
        _referralService = referralService;
    }

    [HttpGet("cart-discounts")]
    [ProducesResponseType(typeof(List<CartReferralDiscountOptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CartReferralDiscountOptionDto>>> GetCartDiscounts()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _referralService.GetCartReferralDiscountOptionsAsync(userId.Value));
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("UserId")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}

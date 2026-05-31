using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Services;

namespace Bebochka.Api.Controllers;

[ApiController]
[Route("api/admin/referrals")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminReferralsController : ControllerBase
{
    private readonly IReferralService _referralService;

    public AdminReferralsController(IReferralService referralService)
    {
        _referralService = referralService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AdminReferralListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AdminReferralListItemDto>>> Search(
        [FromQuery] string? search,
        [FromQuery] string? status)
    {
        var list = await _referralService.SearchReferralsAsync(search, status);
        return Ok(list);
    }
}

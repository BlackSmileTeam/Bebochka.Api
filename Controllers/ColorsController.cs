using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Controller for retrieving available product colors
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ColorsController : ControllerBase
{
    /// <summary>
    /// Gets the list of available colors for products
    /// </summary>
    /// <returns>List of available color names</returns>
    /// <response code="200">Returns the list of colors</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [EnableCors("AllowReactApp")]
    public IActionResult GetColors()
    {
        var colors = new List<string>
        {
            "Белый", "Черный", "Серый", "Бежевый", "Коричневый",
            "Красный", "Розовый", "Оранжевый", "Желтый",
            "Зеленый", "Голубой", "Синий", "Фиолетовый",
            "Многоцветный", "Другой"
        };

        // Explicitly return JSON array
        return new JsonResult(colors)
        {
            StatusCode = 200
        };
    }
}

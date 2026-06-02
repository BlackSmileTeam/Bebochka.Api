namespace Bebochka.Api.Models;

/// <summary>
/// Product saved by user to favorites list.
/// </summary>
public class UserFavoriteProduct
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Product? Product { get; set; }
}

using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Services;

/// <summary>
/// Interface for order operations
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Creates a new order
    /// </summary>
    /// <param name="dto">Order data</param>
    /// <returns>Created order</returns>
    Task<OrderDto> CreateOrderAsync(CreateOrderDto dto);
    
    /// <summary>
    /// Gets all orders
    /// </summary>
    /// <returns>List of orders</returns>
    Task<List<OrderDto>> GetAllOrdersAsync();
    
    /// <summary>
    /// Gets an order by ID
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <returns>Order or null if not found</returns>
    Task<OrderDto?> GetOrderByIdAsync(int id);
    
    /// <summary>
    /// Gets orders by user ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of user orders</returns>
    Task<List<OrderDto>> GetUserOrdersAsync(int userId);
    
    /// <summary>
    /// Cancels an order
    /// </summary>
    /// <param name="orderId">Order ID</param>
    /// <param name="reason">Cancellation reason</param>
    /// <returns>True if cancelled successfully</returns>
    Task<bool> CancelOrderAsync(int orderId, string? reason = null);
    
    /// <summary>
    /// Updates order status
    /// </summary>
    /// <param name="orderId">Order ID</param>
    /// <param name="status">New status</param>
    /// <returns>True if updated successfully</returns>
    Task<bool> UpdateOrderStatusAsync(int orderId, string status);
    
    /// <summary>
    /// Gets order statistics
    /// </summary>
    /// <returns>Statistics</returns>
    Task<OrderStatisticsDto> GetStatisticsAsync();

    /// <summary>
    /// Deletes an order and its items from the database.
    /// </summary>
    /// <param name="orderId">Order ID</param>
    /// <returns>True if deleted</returns>
    Task<bool> DeleteOrderAsync(int orderId);

    /// <summary>
    /// Reserves a product from a Telegram channel post (first allowed comment).
    /// </summary>
    Task<ReserveFromTelegramResultDto> ReserveFromTelegramAsync(string channelId, int messageId, long telegramUserId, string? username, string? firstName, string? lastName, string? customerPhone = null, long? commentChatId = null, int? commentMessageId = null);

    /// <summary>
    /// Removes an item from an order: deletes user's Telegram comment, restores stock, optionally assigns product to next user from reserve queue.
    /// </summary>
    Task<bool> DeleteOrderItemAsync(int orderId, int itemId);

    /// <summary>
    /// Sets whether an order item is marked as added to the parcel.
    /// </summary>
    Task<bool> SetOrderItemAddedToParcelAsync(int orderId, int itemId, bool addedToParcel);

    /// <summary>
    /// Applies discount to multiple orders (fixed percent or by condition).
    /// </summary>
    Task ApplyDiscountToOrdersAsync(IEnumerable<int> orderIds, string discountType, int? fixedPercent, int? condition1, int? condition3, int? condition5Plus);

    /// <summary>
    /// Removes discount from an order.
    /// </summary>
    Task<bool> RemoveOrderDiscountAsync(int orderId);

    /// <summary>
    /// Applies fixed discount to a single order.
    /// </summary>
    Task<bool> ApplyOrderDiscountAsync(int orderId, int percent);
}


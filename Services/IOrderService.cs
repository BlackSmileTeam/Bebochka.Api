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
}


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
}


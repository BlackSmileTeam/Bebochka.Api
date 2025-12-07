using Bebochka.Api.Models;

namespace Bebochka.Api.Services;

/// <summary>
/// Interface for email operations
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends order notification email
    /// </summary>
    /// <param name="order">Order to notify about</param>
    Task SendOrderNotificationAsync(Order order);
}


using System.Net;
using System.Net.Mail;
using Bebochka.Api.Models;

namespace Bebochka.Api.Services;

/// <summary>
/// Service implementation for email operations
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendOrderNotificationAsync(Order order)
    {
        try
        {
            var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["Email:Username"] ?? "";
            var smtpPassword = _configuration["Email:Password"] ?? "";
            var fromEmail = _configuration["Email:FromEmail"] ?? smtpUsername;
            var toEmail = _configuration["Email:ToEmail"] ?? "sekisov@gmail.com";

            if (string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
            {
                _logger.LogWarning("Email credentials not configured. Skipping email send.");
                return;
            }

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword)
            };

            var subject = $"Новый заказ #{order.OrderNumber}";
            var body = BuildOrderEmailBody(order);

            using var message = new MailMessage(fromEmail, toEmail, subject, body)
            {
                IsBodyHtml = true
            };

            await client.SendMailAsync(message);
            _logger.LogInformation($"Order notification email sent for order {order.OrderNumber}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send order notification email for order {order.OrderNumber}");
            throw;
        }
    }

    private static string BuildOrderEmailBody(Order order)
    {
        var itemsHtml = string.Join("", order.OrderItems.Select(item =>
            $@"
            <tr>
                <td style='padding: 8px; border: 1px solid #ddd;'>{item.ProductName}</td>
                <td style='padding: 8px; border: 1px solid #ddd; text-align: center;'>{item.Quantity}</td>
                <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{item.ProductPrice:N2} ₽</td>
                <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{item.ProductPrice * item.Quantity:N2} ₽</td>
            </tr>"));

        return $@"
        <html>
        <body style='font-family: Arial, sans-serif;'>
            <h2>Новый заказ #{order.OrderNumber}</h2>
            <h3>Информация о клиенте:</h3>
            <p><strong>Имя:</strong> {order.CustomerName}</p>
            <p><strong>Телефон:</strong> {order.CustomerPhone}</p>
            {(string.IsNullOrEmpty(order.CustomerEmail) ? "" : $"<p><strong>Email:</strong> {order.CustomerEmail}</p>")}
            {(string.IsNullOrEmpty(order.CustomerAddress) ? "" : $"<p><strong>Адрес:</strong> {order.CustomerAddress}</p>")}
            {(string.IsNullOrEmpty(order.DeliveryMethod) ? "" : $"<p><strong>Способ доставки:</strong> {order.DeliveryMethod}</p>")}
            {(string.IsNullOrEmpty(order.Comment) ? "" : $"<p><strong>Комментарий:</strong> {order.Comment}</p>")}
            
            <h3>Товары в заказе:</h3>
            <table style='width: 100%; border-collapse: collapse;'>
                <thead>
                    <tr style='background-color: #f2f2f2;'>
                        <th style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Товар</th>
                        <th style='padding: 8px; border: 1px solid #ddd; text-align: center;'>Количество</th>
                        <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Цена</th>
                        <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Сумма</th>
                    </tr>
                </thead>
                <tbody>
                    {itemsHtml}
                </tbody>
                <tfoot>
                    <tr>
                        <td colspan='3' style='padding: 8px; border: 1px solid #ddd; text-align: right; font-weight: bold;'>Итого:</td>
                        <td style='padding: 8px; border: 1px solid #ddd; text-align: right; font-weight: bold;'>{order.TotalAmount:N2} ₽</td>
                    </tr>
                </tfoot>
            </table>
            <p><strong>Дата заказа:</strong> {order.CreatedAt:dd.MM.yyyy HH:mm}</p>
        </body>
        </html>";
    }
}


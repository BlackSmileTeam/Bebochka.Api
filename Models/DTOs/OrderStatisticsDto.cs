namespace Bebochka.Api.Models.DTOs;

public class OrderStatisticsDto
{
    public int TotalOrders { get; set; }
    public int FormingOrders { get; set; }
    public int AwaitingPaymentOrders { get; set; }
    public int PendingOrders { get; set; }
    public int OnDeliveryOrders { get; set; }
    public int SentOrders { get; set; }
    /// <summary>Заказы в статусе «Получен» (подтверждение клиентом).</summary>
    public int ReceivedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal PendingRevenue { get; set; }
}


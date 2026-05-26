namespace Bebochka.Api.Models.DTOs;

public class IncomingShipmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public int ItemCount { get; set; }
    public decimal OrderedAmount { get; set; }
    public decimal? Revenue { get; set; }
    public decimal? ActualMargin { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateIncomingShipmentDto
{
    public string Name { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public int ItemCount { get; set; }
    public decimal OrderedAmount { get; set; }
    public string? Notes { get; set; }
}

public class UpdateIncomingShipmentDto
{
    public string Name { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public int ItemCount { get; set; }
    public decimal OrderedAmount { get; set; }
    public string? Notes { get; set; }
}

public class MiscExpenseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? IncomingShipmentId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateMiscExpenseDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? IncomingShipmentId { get; set; }
}

public class UpdateMiscExpenseDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? IncomingShipmentId { get; set; }
}

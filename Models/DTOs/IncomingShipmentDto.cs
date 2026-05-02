namespace Bebochka.Api.Models.DTOs;

public class IncomingShipmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public int ItemCount { get; set; }
    public decimal OrderedAmount { get; set; }
    public decimal? Revenue { get; set; }
    public decimal MiscExpensesTotal { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal? ActualMargin { get; set; }
    public string? Notes { get; set; }
    public List<IncomingShipmentExpenseDto> Expenses { get; set; } = new();
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
    public List<CreateIncomingShipmentExpenseDto>? Expenses { get; set; }
}

public class UpdateIncomingShipmentDto
{
    public string Name { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public int ItemCount { get; set; }
    public decimal OrderedAmount { get; set; }
    public string? Notes { get; set; }
    public List<CreateIncomingShipmentExpenseDto>? Expenses { get; set; }
}

public class IncomingShipmentExpenseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateIncomingShipmentExpenseDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

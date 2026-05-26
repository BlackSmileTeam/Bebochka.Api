namespace Bebochka.Api.Models.DTOs;

public class StartBackupDto
{
    public string DateFrom { get; set; } = "";
    public string DateTo { get; set; } = "";
}

public class BackupProgressDto
{
    public int Percent { get; set; }
    public string Stage { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Error { get; set; }
    public string? FileName { get; set; }
}

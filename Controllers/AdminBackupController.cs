using System.Diagnostics;
using System.IO.Compression;
using Bebochka.Api.Helpers;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace Bebochka.Api.Controllers;

[ApiController]
[Route("api/admin/backup")]
[Authorize(Roles = "Admin")]
public class AdminBackupController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly BackupService _backupService;
    private readonly BackupJobStore _jobStore;

    public AdminBackupController(
        IWebHostEnvironment env,
        IConfiguration config,
        BackupService backupService,
        BackupJobStore jobStore)
    {
        _env = env;
        _config = config;
        _backupService = backupService;
        _jobStore = jobStore;
    }

    [HttpPost("start")]
    public ActionResult Start([FromBody] StartBackupDto dto)
    {
        if (!DateOnly.TryParse(dto.DateFrom, out var dateFrom))
            return BadRequest(new { message = "Укажите корректную дату «с»." });
        if (!DateOnly.TryParse(dto.DateTo, out var dateTo))
            return BadRequest(new { message = "Укажите корректную дату «по»." });
        if (dateTo < dateFrom)
            return BadRequest(new { message = "Дата «по» не может быть раньше даты «с»." });

        try
        {
            var jobId = _backupService.StartJob(dateFrom, dateTo);
            return Ok(new
            {
                jobId,
                dateFrom = dateFrom.ToString("yyyy-MM-dd"),
                dateTo = dateTo.ToString("yyyy-MM-dd")
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("progress/{jobId}")]
    public ActionResult<BackupProgressDto> Progress(string jobId)
    {
        var job = _jobStore.Get(jobId);
        if (job == null)
            return NotFound(new { message = "Задача не найдена." });

        return Ok(new BackupProgressDto
        {
            Percent = job.Percent,
            Stage = job.Stage,
            Status = job.Status.ToString().ToLowerInvariant(),
            Error = job.Error,
            FileName = job.FileName
        });
    }

    [HttpGet("download/{jobId}")]
    public IActionResult Download(string jobId)
    {
        var job = _jobStore.Get(jobId);
        if (job == null)
            return NotFound(new { message = "Задача не найдена." });
        if (job.Status != BackupJobStatus.Completed || string.IsNullOrEmpty(job.ZipPath) || !System.IO.File.Exists(job.ZipPath))
            return BadRequest(new { message = "Архив ещё не готов." });

        var fileName = job.FileName ?? Path.GetFileName(job.ZipPath);
        var stream = new FileStream(job.ZipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 64, FileOptions.DeleteOnClose);
        _jobStore.Remove(jobId);
        return File(stream, "application/zip", fileName);
    }

    [HttpPost("restore")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Restore([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length <= 0)
            return BadRequest(new { message = "Файл бэкапа не найден." });

        var tmpRoot = Path.Combine(Path.GetTempPath(), "bebochka-backup-restore");
        Directory.CreateDirectory(tmpRoot);

        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var zipPath = Path.Combine(tmpRoot, $"restore_{stamp}.zip");
        await using (var fs = System.IO.File.Create(zipPath))
        {
            await file.CopyToAsync(fs, ct);
        }

        var extractDir = Path.Combine(tmpRoot, $"unz_{stamp}");
        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, recursive: true);
        Directory.CreateDirectory(extractDir);
        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

        // v2: JSON + uploads (периодический бэкап)
        var manifestV2 = Path.Combine(extractDir, "manifest.json");
        if (System.IO.File.Exists(manifestV2))
        {
            return BadRequest(new
            {
                message = "Это архив за период (JSON). Полное восстановление из такого архива пока не поддерживается — используйте полный бэкап с дампом БД."
            });
        }

        var cs = _config.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        var b = new MySqlConnectionStringBuilder(cs);

        var dbDir = Path.Combine(extractDir, "db");
        if (!Directory.Exists(dbDir))
            return BadRequest(new { message = "В архиве нет папки db/." });

        var sqlGz = Directory.GetFiles(dbDir, "*.sql.gz", SearchOption.AllDirectories).FirstOrDefault();
        var sql = Directory.GetFiles(dbDir, "*.sql", SearchOption.AllDirectories).FirstOrDefault();
        if (sqlGz == null && sql == null)
            return BadRequest(new { message = "В архиве нет дампа базы (*.sql или *.sql.gz) в db/." });

        if (sqlGz != null)
        {
            await using var inFs = System.IO.File.OpenRead(sqlGz);
            await using var gz = new GZipStream(inFs, CompressionMode.Decompress);
            await RunMySqlImportFromStreamAsync(b, gz, ct);
        }
        else
        {
            await using var inFs = System.IO.File.OpenRead(sql!);
            await RunMySqlImportFromStreamAsync(b, inFs, ct);
        }

        var uploadsFrom = Path.Combine(extractDir, "uploads");
        if (Directory.Exists(uploadsFrom))
        {
            var uploadsTo = Path.Combine(AppPaths.WwwRoot(_env), "uploads");
            Directory.CreateDirectory(Path.GetDirectoryName(uploadsTo)!);

            if (Directory.Exists(uploadsTo))
            {
                var backupOld = Path.Combine(AppPaths.WwwRoot(_env), $"uploads_before_restore_{stamp}");
                Directory.Move(uploadsTo, backupOld);
            }
            CopyDirectory(uploadsFrom, uploadsTo);
        }

        return Ok(new { message = "Бэкап восстановлен." });
    }

    private static async Task RunMySqlImportFromStreamAsync(MySqlConnectionStringBuilder b, Stream input, CancellationToken ct)
    {
        var args = string.Join(' ', new[]
        {
            "-h", Quote(b.Server),
            "-P", b.Port.ToString(),
            "-u", Quote(b.UserID),
            Quote(b.Database)
        });

        var psi = new ProcessStartInfo("mysql", args)
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        if (!string.IsNullOrEmpty(b.Password))
            psi.Environment["MYSQL_PWD"] = b.Password;

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Не удалось запустить mysql.");
        await input.CopyToAsync(p.StandardInput.BaseStream, ct);
        await p.StandardInput.FlushAsync();
        p.StandardInput.Close();
        var err = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"mysql import failed: {err}");
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(from, file);
            var dst = Path.Combine(to, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            System.IO.File.Copy(file, dst, overwrite: true);
        }
    }

    private static string Quote(string? s)
    {
        s ??= "";
        return s.Contains(' ') ? $"\"{s.Replace("\"", "\\\"")}\"" : s;
    }
}

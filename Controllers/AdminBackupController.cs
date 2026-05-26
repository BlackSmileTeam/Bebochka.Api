using System.Diagnostics;
using System.IO.Compression;
using Bebochka.Api.Helpers;
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

    public AdminBackupController(IWebHostEnvironment env, IConfiguration config)
    {
        _env = env;
        _config = config;
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download(CancellationToken ct)
    {
        var tmpRoot = Path.Combine(Path.GetTempPath(), "bebochka-backup");
        Directory.CreateDirectory(tmpRoot);
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");

        var zipPath = Path.Combine(tmpRoot, $"bebochka_backup_{stamp}.zip");
        if (System.IO.File.Exists(zipPath))
            System.IO.File.Delete(zipPath);

        var uploadsDir = Path.Combine(AppPaths.WwwRoot(_env), "uploads");
        if (!Directory.Exists(uploadsDir))
            Directory.CreateDirectory(uploadsDir);

        var cs = _config.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        var b = new MySqlConnectionStringBuilder(cs);

        await using (var zipFs = System.IO.File.Create(zipPath))
        using (var zip = new ZipArchive(zipFs, ZipArchiveMode.Create, leaveOpen: false))
        {
            // DB dump (gz)
            var dbEntry = zip.CreateEntry($"db/{b.Database}_{stamp}.sql.gz", CompressionLevel.Optimal);
            await using (var entryStream = dbEntry.Open())
            await using (var gz = new GZipStream(entryStream, CompressionLevel.Optimal, leaveOpen: false))
            {
                await RunMySqlDumpToStreamAsync(b, gz, ct);
            }

            // uploads/
            AddDirectoryToZip(zip, uploadsDir, "uploads");

            // manifest
            var manifest = zip.CreateEntry("manifest.txt", CompressionLevel.Optimal);
            await using (var ms = new StreamWriter(manifest.Open()))
            {
                await ms.WriteLineAsync($"created_utc={DateTime.UtcNow:O}");
                await ms.WriteLineAsync($"db={b.Database}");
                await ms.WriteLineAsync($"uploads_dir={uploadsDir}");
            }
        }

        var outName = Path.GetFileName(zipPath);
        var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 64, FileOptions.DeleteOnClose);
        return File(stream, "application/zip", outName);
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

        var cs = _config.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        var b = new MySqlConnectionStringBuilder(cs);

        // restore db: pick first *.sql or *.sql.gz under db/
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

        // restore uploads: replace uploads dir
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

    private static async Task RunMySqlDumpToStreamAsync(MySqlConnectionStringBuilder b, Stream output, CancellationToken ct)
    {
        var args = string.Join(' ', new[]
        {
            "-h", Quote(b.Server),
            "-P", b.Port.ToString(),
            "-u", Quote(b.UserID),
            Quote(b.Database),
            "--single-transaction",
            "--quick",
            "--routines",
            "--triggers",
            "--events"
        });

        var psi = new ProcessStartInfo("mysqldump", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        if (!string.IsNullOrEmpty(b.Password))
            psi.Environment["MYSQL_PWD"] = b.Password;

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Не удалось запустить mysqldump.");
        await p.StandardOutput.BaseStream.CopyToAsync(output, ct);
        var err = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"mysqldump failed: {err}");
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

    private static void AddDirectoryToZip(ZipArchive zip, string dir, string zipRoot)
    {
        var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
        foreach (var f in files)
        {
            var rel = Path.GetRelativePath(dir, f).Replace('\\', '/');
            var entry = zip.CreateEntry($"{zipRoot}/{rel}", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            using var fs = System.IO.File.OpenRead(f);
            fs.CopyTo(entryStream);
        }
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


using System.Collections.Concurrent;

namespace Bebochka.Api.Services;

public enum BackupJobStatus
{
    Running,
    Completed,
    Failed
}

public class BackupJob
{
    public string Id { get; init; } = "";
    public int Percent { get; set; }
    public string Stage { get; set; } = "";
    public BackupJobStatus Status { get; set; } = BackupJobStatus.Running;
    public string? Error { get; set; }
    public string? ZipPath { get; set; }
    public string? FileName { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

public class BackupJobStore
{
    private readonly ConcurrentDictionary<string, BackupJob> _jobs = new();

    public BackupJob Create(string id)
    {
        var job = new BackupJob { Id = id };
        _jobs[id] = job;
        return job;
    }

    public BackupJob? Get(string id) => _jobs.TryGetValue(id, out var j) ? j : null;

    public void SetProgress(string id, int percent, string stage)
    {
        if (!_jobs.TryGetValue(id, out var job) || job.Status != BackupJobStatus.Running) return;
        job.Percent = Math.Clamp(percent, 0, 99);
        job.Stage = stage;
    }

    public void Complete(string id, string zipPath, string fileName)
    {
        if (!_jobs.TryGetValue(id, out var job)) return;
        job.ZipPath = zipPath;
        job.FileName = fileName;
        job.Percent = 100;
        job.Stage = "Готово";
        job.Status = BackupJobStatus.Completed;
    }

    public void Fail(string id, string error)
    {
        if (!_jobs.TryGetValue(id, out var job)) return;
        job.Error = error;
        job.Status = BackupJobStatus.Failed;
        job.Stage = "Ошибка";
    }

    public bool Remove(string id)
    {
        if (!_jobs.TryRemove(id, out var job)) return false;
        if (job.ZipPath != null && File.Exists(job.ZipPath))
        {
            try { File.Delete(job.ZipPath); } catch { /* ignore */ }
        }
        return true;
    }
}

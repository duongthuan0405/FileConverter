namespace FileConverterService.Application.Jobs;

public interface IJobCleanupService
{
    /// <summary>Automatic deletion is called only for due schedule members; manual deletion reports missing or active jobs.</summary>
    Task<bool> ExecuteAsync(Guid jobId, bool automatic = false, CancellationToken cancellationToken = default);
}

namespace FileConverterService.Application.Readiness;

public sealed record ReadinessResult(IReadOnlyDictionary<string, bool> Checks)
{
    public bool Ready => Checks.Values.All(ready => ready);
}

public interface IReadinessService
{
    Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default);
}

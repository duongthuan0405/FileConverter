using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Readiness;
using FileConverterService.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FileConverterService.Infrastructure.Readiness;

public sealed class ReadinessService(IConnectionMultiplexer redis, IFileManagerService files,
    HttpClient http, IOptions<ReadinessOptions> options, ILogger<ReadinessService> logger) : IReadinessService
{
    public async Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        var checks = await Task.WhenAll(
            ProbeAsync("redis", async token => { await redis.GetDatabase().PingAsync().WaitAsync(token); }, deadline.Token, cancellationToken),
            ProbeAsync("storage", CheckStorageAsync, deadline.Token, cancellationToken),
            ProbeAsync("gotenberg", async token =>
            {
                using var response = await http.GetAsync("/health", HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
            }, deadline.Token, cancellationToken));
        return new ReadinessResult(checks.ToDictionary(check => check.Name, check => check.Ready));
    }

    private async Task<(string Name, bool Ready)> ProbeAsync(string name, Func<CancellationToken, Task> check,
        CancellationToken deadline, CancellationToken caller)
    {
        try
        {
            await check(deadline);
            deadline.ThrowIfCancellationRequested();
            return (name, true);
        }
        catch (Exception exception) when (!caller.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Readiness check {Dependency} failed.", name);
            return (name, false);
        }
    }

    private async Task CheckStorageAsync(CancellationToken token)
    {
        // Test both directories used by conversion, including atomic publication and deletion.
        foreach (var directory in new[] { "input", "output" })
        {
            var key = $"{directory}/.readiness-{Guid.NewGuid():N}";
            try
            {
                using var content = new MemoryStream([42]);
                await files.SaveFileAtomicallyAsync(content, key, token);
                await using var saved = await files.ReadFileAsync(key, token);
                var buffer = new byte[1];
                if (await saved.ReadAsync(buffer, token) != 1 || buffer[0] != 42)
                    throw new IOException("Storage probe could not read the written content.");
            }
            finally
            {
                // Do not leave probe files behind just because its request was canceled.
                await files.DeleteFileAsync(key, CancellationToken.None);
            }
        }
    }
}

using FileConverterService.Application.Converters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileConverterService.Infrastructure.Converters;

public sealed class GotenbergDocumentConverter : IDocumentConverter
{
    private readonly HttpClient _httpClient;
    private readonly GotenbergOptions _options;
    private readonly ILogger<GotenbergDocumentConverter> _logger;

    public GotenbergDocumentConverter(
        HttpClient httpClient,
        IOptions<GotenbergOptions> options,
        ILogger<GotenbergDocumentConverter> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ConvertToPdfAsync(
        Guid jobId,
        Stream input,
        string fileName,
        Func<Stream, Task>? outputWriter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _logger.LogInformation("Starting PDF conversion for job {JobId}.", jobId);
        using var response = await SendWithRetryAsync(jobId, input, fileName, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var pdfStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (outputWriter is not null)
        {
            await outputWriter(pdfStream);
        }

        _logger.LogInformation("PDF conversion completed for job {JobId}.", jobId);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Guid jobId,
        Stream input,
        string fileName,
        CancellationToken cancellationToken)
    {


        TimeSpan delayTimeSpan = new TimeSpan(0, _options.RetryDelayInMinutes, 0);
        int maxAttempts = _options.MaxAttempts;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (input.CanSeek)
            {
                input.Position = 0;
            }


            try
            {
                _logger.LogDebug(
                    "Sending Gotenberg request for job {JobId}. Attempt {Attempt}/{MaxAttempts}.",
                    jobId, attempt, maxAttempts);

                using var content = new MultipartFormDataContent();
                content.Add(new StreamContent(input), "files", fileName);

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "/forms/libreoffice/convert")
                {
                    Content = content
                };

                var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if ((int)response.StatusCode >= 500 && attempt < _options.MaxAttempts)
                {
                    _logger.LogWarning(
                        "Gotenberg returned HTTP {StatusCode} for job {JobId}. Retrying in {RetryDelay}.",
                        (int)response.StatusCode, jobId, delayTimeSpan);
                    response.Dispose();
                    await Task.Delay(delayTimeSpan, cancellationToken);
                    continue;
                }

                return response;
            }
            catch (HttpRequestException) when (attempt < _options.MaxAttempts)
            {
                _logger.LogWarning(
                    "Gotenberg request failed for job {JobId}. Retrying in {RetryDelay}.",
                    jobId, delayTimeSpan);
                await Task.Delay(delayTimeSpan, cancellationToken);
            }
            catch (TaskCanceledException) when (
                !cancellationToken.IsCancellationRequested && attempt < _options.MaxAttempts)
            {
                _logger.LogWarning(
                    "Gotenberg request timed out for job {JobId}. Retrying in {RetryDelay}.",
                    jobId, delayTimeSpan);
                await Task.Delay(delayTimeSpan, cancellationToken);
            }
        }

        _logger.LogError(
            "Gotenberg conversion failed for job {JobId} after {MaxAttempts} attempts.",
            jobId, maxAttempts);

        throw new InvalidOperationException("Gotenberg request failed after all retry attempts.");
    }
}

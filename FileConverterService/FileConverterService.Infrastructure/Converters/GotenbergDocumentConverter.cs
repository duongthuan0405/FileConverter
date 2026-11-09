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
        Func<Stream, CancellationToken, Task>? callBack,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCts.CancelAfter(TimeSpan.FromMinutes(_options.DeadlineInMinutes));
        var deadlineToken = deadlineCts.Token;

        try
        {
            deadlineToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Starting PDF conversion for job {JobId}.", jobId);
            using var response = await SendWithRetryAsync(jobId, input, fileName, deadlineToken);

            response.EnsureSuccessStatusCode();

            await using var pdfStream = await response.Content.ReadAsStreamAsync(deadlineToken);
            if (callBack is not null)
            {
                await callBack(pdfStream, deadlineToken);
            }

            deadlineToken.ThrowIfCancellationRequested();
            _logger.LogInformation("PDF conversion completed for job {JobId}.", jobId);
        }
        catch (OperationCanceledException exception) when (
            deadlineCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("PDF conversion for job {JobId} exceeded the total deadline of {DeadlineMinutes} minutes.",
                jobId, _options.DeadlineInMinutes);
            throw new TimeoutException(
                $"PDF conversion exceeded the total deadline of {_options.DeadlineInMinutes} minutes.", exception);
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Guid jobId,
        Stream input,
        string fileName,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMinutes(_options.RetryDelayInMinutes);
        int maxAttempts = input.CanSeek ? _options.MaxAttempts : 1;
        long initialPosition = input.CanSeek ? input.Position : 0;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (input.CanSeek)
                input.Position = initialPosition;

            try
            {
                _logger.LogDebug(
                    "Sending Gotenberg request for job {JobId}. Attempt {Attempt}/{MaxAttempts}.",
                    jobId, attempt, maxAttempts);

                using var content = new MultipartFormDataContent();
                content.Add(new StreamContent(new LeaveOpenReadStream(input)), "files", fileName);
                using var request = new HttpRequestMessage(HttpMethod.Post, "/forms/libreoffice/convert")
                {
                    Content = content
                };

                var response = await _httpClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if ((int)response.StatusCode >= 500 && attempt < maxAttempts)
                {
                    _logger.LogWarning(
                        "Gotenberg returned HTTP {StatusCode} for job {JobId}. Retrying in {RetryDelay}.",
                        (int)response.StatusCode, jobId, delay);
                    response.Dispose();
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                return response;
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    "Gotenberg request failed for job {JobId}. Retrying in {RetryDelay}.", jobId, delay);
                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                _logger.LogWarning(
                    "Gotenberg request timed out for job {JobId}. Retrying in {RetryDelay}.", jobId, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Gotenberg request failed after all retry attempts.");
    }

    private sealed class LeaveOpenReadStream : Stream
    {
        private readonly Stream _inner;

        public LeaveOpenReadStream(Stream inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        // Stream.Dispose only disposes this wrapper; it does not know about inner.
    }
}

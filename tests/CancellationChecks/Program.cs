using FileConverterService.Api.Middlewares;
using FileConverterService.Application.Jobs;
using FileConverterService.Application.UseCases.ConvertToPDF;
using FileConverterService.Infrastructure.HelperServices.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var root = Path.Combine(Path.GetTempPath(), "fileconverter-cancellation-" + Guid.NewGuid().ToString("N"));
var storage = new LocalFileManagerService(Options.Create(new StorageOptions { RootPath = root }));
try
{
    using var canceled = new CancellationTokenSource();
    canceled.Cancel();
    await ExpectCancellation(() => storage.SaveFileAsync(new MemoryStream([1]), "input/canceled.docx", canceled.Token));
    Check(!Directory.EnumerateFileSystemEntries(root).Any(), "Pre-canceled save must not create files or directories.");

    using var duringCopy = new CancellationTokenSource();
    await storage.SaveFileAsync(new MemoryStream([42]), "input/existing.docx");
    await ExpectCancellation(() => storage.SaveFileAtomicallyAsync(
        new CancelOnCopyStream(duringCopy), "input/existing.docx", duringCopy.Token));
    await using (var existing = await storage.ReadFileAsync("input/existing.docx"))
        Check(existing.ReadByte() == 42, "Canceled atomic save must preserve the existing file.");
    Check(!Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).Any(), "Canceled copy must remove temporary files.");

    var operations = new RecordingAtomicOperations();
    var handler = new ConverToPDF_UCHandler(storage, operations,
        NullLogger<ConverToPDF_UCHandler>.Instance);
    await ExpectCancellation(() => handler.Handle(new ConverToPDF_UCInput(), canceled.Token));
    Check(operations.Count == 0, "Pre-canceled handler must not submit.");

    using var atAcceptance = new CancellationTokenSource();
    operations = new RecordingAtomicOperations(atAcceptance);
    handler = new ConverToPDF_UCHandler(storage, operations, NullLogger<ConverToPDF_UCHandler>.Instance);
    var result = await handler.Handle(new ConverToPDF_UCInput
    {
        Files = [new FileParams { FileName = "accepted.docx", Type = "application/msword", Content = new MemoryStream([1, 2]) }]
    }, atAcceptance.Token);
    Check(result.SuccessfulUploadedFile == 1 && operations.Count == 1,
        "Cancellation after acceptance starts must not interrupt submit.");

    var context = new DefaultHttpContext { RequestAborted = canceled.Token };
    await new GlobalExceptionHandlerMiddleware(NullLogger<GlobalExceptionHandlerMiddleware>.Instance)
        .InvokeAsync(context, _ => Task.FromCanceled(canceled.Token));
    Check(context.Response.StatusCode == 499, "Client cancellation must not become HTTP 500.");
    Console.WriteLine("PASS: pre-canceled save, canceled atomic copy, pre-canceled handler, acceptance boundary, client-abort middleware.");
}
finally
{
    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        File.Delete(file);
    foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length))
        Directory.Delete(directory);
    Directory.Delete(root);
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static async Task ExpectCancellation(Func<Task> action)
{
    try { await action(); }
    catch (OperationCanceledException) { return; }
    throw new Exception("Expected cancellation.");
}

sealed class CancelOnCopyStream(CancellationTokenSource source) : MemoryStream
{
    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        await destination.WriteAsync(new byte[] { 9 }, cancellationToken);
        source.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
    }
}

sealed class RecordingAtomicOperations(CancellationTokenSource? source = null) : IConversionJobAtomicOperations
{
    public Task AcknowledgeAsync(FileConverterService.Application.Queues.ConversionJobClaim claim, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public Task<FileConverterService.Application.Queues.ConversionJobClaim> ClaimAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public int Count { get; private set; }
    public Task SubmitAsync(FileConverterService.Domain.Entities.ConversionJob job, CancellationToken cancellationToken = default)
    {
        source?.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
        Count++;
        return Task.CompletedTask;
    }
}

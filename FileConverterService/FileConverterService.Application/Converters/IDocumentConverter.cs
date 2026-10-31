namespace FileConverterService.Application.Converters;

public interface IDocumentConverter
{
    Task ConvertToPdfAsync(
        Guid jobId,
        Stream input,
        string fileName,
        Func<Stream, Task>? outputWriter,
        CancellationToken cancellationToken = default);
}

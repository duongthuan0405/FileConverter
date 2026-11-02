namespace FileConverterService.Application.Converters;

public interface IDocumentConverter
{
    Task ConvertToPdfAsync(
        Guid jobId,
        Stream input,
        string fileName,
        Func<Stream, CancellationToken, Task>? callBack,
        CancellationToken cancellationToken = default);
}

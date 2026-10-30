using FileConverterService.Application.HelperServices.Storage;

namespace FileConverterService.Infrastructure.HelperServices.Storage
{
    public class LocalFileStorageService : IFileStorageService
    {
        public Task<string> SaveFileAsync(Stream fileStream, string relativeFilePath, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> ReadFileAsync(string relativeFilePath, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteFileAsync(string relativeFilePath, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsAsync(string relativeFilePath, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}


using FileConverterService.Application.HelperServices.Storage;
using Microsoft.Extensions.Options;

namespace FileConverterService.Infrastructure.HelperServices.Storage
{
    public class LocalFileManagerService : IFileManagerService
    {
        private readonly string _storageRoot;

        public LocalFileManagerService(IOptions<StorageOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.RootPath);

            _storageRoot = Path.GetFullPath(options.Value.RootPath);
            Directory.CreateDirectory(_storageRoot);
        }

        public async Task<bool> DeleteFileAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = ResolvePath(key);
            if (!File.Exists(filePath))
            {
                return false;
            }

            File.Delete(filePath);
            await Task.CompletedTask;
            return true;
        }

        public Task<bool> ExistsAsync(
            string relativeFilePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(File.Exists(ResolvePath(relativeFilePath)));
        }

        public Task<Stream> ReadFileAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Stream stream = new FileStream(
                ResolvePath(key),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);

            return Task.FromResult(stream);
        }

        public async Task<string> SaveFileAsync(
            Stream fileStream,
            string? key = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fileStream);

            key ??= Guid.NewGuid().ToString("N");
            var filePath = ResolvePath(key);
            var directory = Path.GetDirectoryName(filePath);

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            await using var outputStream = new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true);

            await fileStream.CopyToAsync(outputStream, cancellationToken);
            return key;
        }

        private string ResolvePath(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var rootPath = Path.GetFullPath(_storageRoot);
            var filePath = Path.GetFullPath(Path.Combine(rootPath, key));
            var rootWithSeparator = rootPath.TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!filePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The file key must point inside the storage root.", nameof(key));
            }

            return filePath;
        }
    }
}


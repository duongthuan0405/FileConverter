namespace FileConverterService.Application.HelperServices.Storage
{
    public interface IFileStorageService
    {
        /// <summary>
        /// Lưu stream file vào vùng lưu trữ tạm theo fileName hoặc jobId.
        /// </summary>
        /// <param name="fileStream">Nội dung tệp tin</param>
        /// <param name="relativeFilePath">Đường dẫn tương đối hoặc tên file lưu trữ</param>
        /// <param name="cancellationToken">Token hủy tác vụ</param>
        /// <returns>Đường dẫn tuyệt đối hoặc định danh tệp đã lưu</returns>
        Task<string> SaveFileAsync(Stream fileStream, string relativeFilePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Đọc tệp tin từ vùng lưu trữ.
        /// </summary>
        /// <param name="relativeFilePath">Đường dẫn tương đối hoặc tên file cần đọc</param>
        /// <param name="cancellationToken">Token hủy tác vụ</param>
        /// <returns>Stream đọc tệp</returns>
        Task<Stream> ReadFileAsync(string relativeFilePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Xóa tệp tin khỏi vùng lưu trữ.
        /// </summary>
        /// <param name="relativeFilePath">Đường dẫn tương đối hoặc tên file cần xóa</param>
        /// <param name="cancellationToken">Token hủy tác vụ</param>
        /// <returns>True nếu xóa thành công, False nếu tệp không tồn tại</returns>
        Task<bool> DeleteFileAsync(string relativeFilePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Kiểm tra sự tồn tại của tệp tin.
        /// </summary>
        /// <param name="relativeFilePath">Đường dẫn tương đối hoặc tên file cần kiểm tra</param>
        /// <param name="cancellationToken">Token hủy tác vụ</param>
        Task<bool> ExistsAsync(string relativeFilePath, CancellationToken cancellationToken = default);
    }
}


using System.IO;

namespace FileConverterService.Application.UseCases.GetFileOfJob
{
    public class GetFileOfJob_UCOutput
    {
        /// <summary>Stream PDF; bên trả response chịu trách nhiệm dispose sau khi gửi xong.</summary>
        public Stream Content { get; set; } = Stream.Null;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
    }
}


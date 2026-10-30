using MediatR;

namespace FileConverterService.Application.UseCases.ConvertToPDF
{
    public class ConverToPDF_UCInput : IRequest<ConverToPDF_UCOutput>
    {
        
        public IEnumerable<FileParams> Files { get; set; } = new List<FileParams>();
    }

    public class FileParams
    {
        public string FileName { get; set; } = string.Empty;
        public string Type {  get; set; } = string.Empty;
        public Stream Content { get; set; } = null!;
    }
}

using MediatR;

namespace FileConverterService.Application.UseCases.GetFileOfJob
{
    public class GetFileOfJob_UCInput : IRequest<GetFileOfJob_UCOutput>
    {
        public string JobId { get; set; } = string.Empty;
    }
}


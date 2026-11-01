using MediatR;

namespace FileConverterService.Application.UseCases.GetJob
{
    public class GetJob_UCInput : IRequest<GetJob_UCOutput>
    {
        public string JobId { get; set; } = string.Empty;
    }
}


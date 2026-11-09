using MediatR;

namespace FileConverterService.Application.UseCases.DeleteResourceRelatedToJob
{
    public class DeleteResourceRelatedToJob_UCInput : IRequest<DeleteResourceRelatedToJob_UCOutput>
    {
        public string JobId { get; set; } = string.Empty;
    }

    
}


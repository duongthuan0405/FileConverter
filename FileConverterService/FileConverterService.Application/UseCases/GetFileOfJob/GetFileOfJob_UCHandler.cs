using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileConverterService.Application.UseCases.GetFileOfJob;

public class GetFileOfJob_UCHandler : IRequestHandler<GetFileOfJob_UCInput, GetFileOfJob_UCOutput>
{
    private readonly IJobConversionRepository _jobRepo;
    private readonly IFileManagerService _fileManager;
    private readonly ILogger<GetFileOfJob_UCHandler> _logger;

    public GetFileOfJob_UCHandler(IJobConversionRepository jobRepo, IFileManagerService fileManager, ILogger<GetFileOfJob_UCHandler> logger)
    {
        _jobRepo = jobRepo;
        _fileManager = fileManager;
        _logger = logger;
    }

    public async Task<GetFileOfJob_UCOutput> Handle(GetFileOfJob_UCInput request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}


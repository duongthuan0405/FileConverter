using FileConverterService.Domain.Entities;

namespace FileConverterService.Application.Repositories;

public interface IJobConversionRepository
{
    Task<ConversionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(ConversionJob job, CancellationToken cancellationToken = default);

    Task UpdateAsync(ConversionJob job, CancellationToken cancellationToken = default);

    Task DeleteAsync(ConversionJob job, CancellationToken cancellationToken = default);
}

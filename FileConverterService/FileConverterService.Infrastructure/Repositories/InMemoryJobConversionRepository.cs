using System.Collections.Concurrent;
using FileConverterService.Domain.Entities;
using FileConverterService.Application.Repositories;

namespace FileConverterService.Infrastructure.Repositories;

public sealed class InMemoryJobConversionRepository : IJobConversionRepository
{
    private readonly ConcurrentDictionary<Guid, ConversionJob> _jobs = new();
    public Task<ConversionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) 
    { 
        cancellationToken.ThrowIfCancellationRequested();
        _jobs.TryGetValue(id, out var job); 
        return Task.FromResult(job); 
    }
    public Task AddAsync(ConversionJob job, CancellationToken cancellationToken = default) 
    { 
        ArgumentNullException.ThrowIfNull(job); 
        cancellationToken.ThrowIfCancellationRequested(); 
        if (!_jobs.TryAdd(job.Id, job))
            throw new InvalidOperationException($"Job '{job.Id}' already exists.");

        Console.WriteLine("Done!");
        return Task.CompletedTask; 
    }
    public Task UpdateAsync(ConversionJob job, CancellationToken cancellationToken = default) 
    { 
        ArgumentNullException.ThrowIfNull(job); 
        cancellationToken.ThrowIfCancellationRequested(); 
        _jobs[job.Id] = job; 
        return Task.CompletedTask; 
    }
    public Task DeleteAsync(ConversionJob job, CancellationToken cancellationToken = default) 
    { 
        ArgumentNullException.ThrowIfNull(job); 
        cancellationToken.ThrowIfCancellationRequested(); 
        _jobs.TryRemove(job.Id, out _); 
        return Task.CompletedTask; 
    }
}

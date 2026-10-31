using FileConverterService.Application.Repositories;
using FileConverterService.Domain.Entities;
using FileConverterService.Infrastructure.Extensions;
using FileConverterService.Infrastructure.Queues;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace FileConverterService.Infrastructure.Repositories
{
    public class RedisJobConversionRepository : IJobConversionRepository
    {
        private readonly IDatabase _redisDb;
        private readonly RedisKey _batchKey;
        private readonly RedisKey _jobKey;

        public RedisJobConversionRepository(IConnectionMultiplexer connection, IOptions<RedisOptions> options)
        {
            _redisDb = connection.GetDatabase();
            _batchKey = options.Value.BatchKey;
            _jobKey = options.Value.JobKey;
        }

        public async Task AddAsync(ConversionJob job, CancellationToken cancellationToken = default)
        {
            await _redisDb.HashSetObjectAsync($"{_jobKey}:{job.Id}", job, cancellationToken);
        }

        public async Task DeleteAsync(ConversionJob job, CancellationToken cancellationToken = default)
        {
            await _redisDb.KeyDeleteAsync($"{_jobKey}:{job.Id}").WaitAsync(cancellationToken);
        }

        public async Task<ConversionJob?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            string key = $"{_jobKey}:{id}";
            return await _redisDb.HashGetToObjectAsync<ConversionJob>(key, cancellationToken);
            
        }

        public async Task UpdateAsync(
            ConversionJob job,
            CancellationToken cancellationToken = default)
        {
            RedisKey key = $"{_jobKey}:{job.Id}";

            bool exists = await _redisDb
                .KeyExistsAsync(key)
                .WaitAsync(cancellationToken);

            if (!exists)
            {
                throw new KeyNotFoundException($"ConversionJob {job.Id} not found.");
            }


            await _redisDb.HashSetObjectAsync(key, job, cancellationToken);
     
        }

    }
}

using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace FileConverterService.Infrastructure.Extensions
{
    internal static partial class RedisHashExtensions
    {
        public static async Task HashSetObjectAsync<T>(this IDatabase db, RedisKey key, T obj, CancellationToken cancellationToken) where T : class
        {
            var hashEntries = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .Select(p =>
                {
                    object? value = p.GetValue(obj);

                    return new HashEntry(
                        p.Name,
                        JsonSerializer.Serialize(value, p.PropertyType)
                    );
                })
                .ToArray();

            if(hashEntries == null)
            {
                throw new ArgumentNullException("Object need stored to Redis must be not null");
            }

            await db.HashSetAsync(key, hashEntries).WaitAsync(cancellationToken).WaitAsync(cancellationToken);
        }    
    }
}

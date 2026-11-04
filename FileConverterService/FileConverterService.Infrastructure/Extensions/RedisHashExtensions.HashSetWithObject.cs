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
            var hashEntries = ToHashEntries(obj);
            await db.HashSetAsync(key, hashEntries).WaitAsync(cancellationToken);
        }

        internal static HashEntry[] ToHashEntries<T>(T obj) where T : class
        {
            ArgumentNullException.ThrowIfNull(obj);
            return typeof(T)
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

        }    
    }
}

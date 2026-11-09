using FileConverterService.Domain.Entities;
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
        public static async Task<T?> HashGetToObjectAsync<T>(this IDatabase db, RedisKey key, CancellationToken cancellationToken) where T : class, new()
        {
            HashEntry[] entries = await db.HashGetAllAsync(key).WaitAsync(cancellationToken);

            if (entries.Length == 0)
                return default(T);

            return FromHashEntries<T>(entries);

        }

        private static T? FromHashEntries<T>(HashEntry[] entries) where T : class, new() 
        {
            if (entries.Length == 0)
                return default(T);

            T obj = new T();

            var properties = typeof(T)
                .GetProperties(
                    BindingFlags.Public | BindingFlags.Instance
                )
                .Where(p =>
                    p.GetIndexParameters().Length == 0)
                // Resolve on the declaring type to retain private setters inherited
                // from a base entity (notably Entity.Id).
                .Select(p => p.DeclaringType!.GetProperty(p.Name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!)
                .Where(p => p.GetSetMethod(nonPublic: true) != null)
                .ToDictionary(p => p.Name);

            foreach (var entry in entries)
            {
                string fieldName = entry.Name.ToString();

                if (!properties.TryGetValue(
                    fieldName, out var property))
                {
                    continue;
                }

                object? value = JsonSerializer.Deserialize(
                    entry.Value.ToString(),
                    property.PropertyType
                );

                property.SetValue(obj, value);
            }

            return obj;
        }
    }
}

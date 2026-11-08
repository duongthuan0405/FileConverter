using FileConverterService.Application.Converters;
using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Jobs;
using FileConverterService.Application.Repositories;
using FileConverterService.Infrastructure.Converters;
using FileConverterService.Infrastructure.HelperServices.Storage;
using FileConverterService.Infrastructure.Options;
using FileConverterService.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FileConverterService.Api.Registrations;

public static class HelperServicesRegistration
{
    public static IServiceCollection RegisterHelperServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileManagerService, LocalFileManagerService>();
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(options.ConnectionString);
        });
        services.AddSingleton<IConversionJobAtomicOperations, RedisConversionJobAtomicOperations>();
        services.AddSingleton<IJobCleanupService, JobCleanupService>();
        services.AddSingleton<IJobCleanupSchedule, FileConverterService.Infrastructure.Repositories.RedisJobCleanupSchedule>();
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<FileConverterService.Application.Readiness.IReadinessService,
            FileConverterService.Infrastructure.Readiness.ReadinessService>()
            .ConfigureHttpClient((provider, client) =>
            {
                client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<GotenbergOptions>>().Value.BaseUrl);
                client.Timeout = Timeout.InfiniteTimeSpan; // Shared readiness deadline controls the HTTP probe.
            });
        services.AddHttpClient<IDocumentConverter, GotenbergDocumentConverter>()
        .ConfigureHttpClient((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GotenbergOptions>>().Value;
            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                throw new InvalidOperationException(
                    "GOTENBERG:BaseUrl must be a valid absolute URL.");
            }

            client.BaseAddress = baseUri;
            client.Timeout = TimeSpan.FromMinutes(options.TimeoutInMinutes);
        });
        return services;
    }
}

using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Queues;
using FileConverterService.Application.Converters;
using FileConverterService.Infrastructure.HelperServices.Storage;
using FileConverterService.Infrastructure.Converters;
using FileConverterService.Infrastructure.Queues;
using Microsoft.Extensions.Options;

namespace FileConverterService.Api.Registrations;

public static class HelperServicesRegistration
{
    public static IServiceCollection RegisterHelperServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileManagerService, LocalFileManagerService>();
        services.AddSingleton<IConversionJobQueue, ChannelConversionQueue>();
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

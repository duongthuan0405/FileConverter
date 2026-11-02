using FileConverterService.Infrastructure.Processors;

namespace FileConverterService.Api.Registrations;

public static class WorkersRegistration
{
    public static IServiceCollection RegisterWorkers(this IServiceCollection services)
    {
        services.AddHostedService<ConversionJobProccessor>();
        return services;
    }
}

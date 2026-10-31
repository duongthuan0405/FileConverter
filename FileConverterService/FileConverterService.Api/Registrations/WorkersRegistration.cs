using FileConverterService.Infrastructure.Workers;

namespace FileConverterService.Api.Registrations;

public static class WorkersRegistration
{
    public static IServiceCollection RegisterWorkers(this IServiceCollection services)
    {
        services.AddHostedService<ConversionWorker>();
        return services;
    }
}

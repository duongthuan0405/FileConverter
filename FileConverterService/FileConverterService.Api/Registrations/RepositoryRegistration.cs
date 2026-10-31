using FileConverterService.Application.UseCases.ConvertToPDF;
using FileConverterService.Infrastructure.Repositories;
using FileConverterService.Application.Repositories;


namespace FileConverterService.Api.Registrations;

public static class RepositoryRegistration
{
    public static IServiceCollection RegisterRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IJobConversionRepository, InMemoryJobConversionRepository>();
        return services;
    }
}

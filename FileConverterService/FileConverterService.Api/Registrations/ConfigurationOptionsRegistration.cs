using FileConverterService.Infrastructure.HelperServices.Storage;
using FileConverterService.Infrastructure.Converters;
using Microsoft.Extensions.Options;

namespace FileConverterService.Api.Registrations;

public static class ConfigurationOptionsRegistration
{
    public static IServiceCollection RegisterConfigurationOptions(
        this IServiceCollection services,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Configure(options => options.RootPath = Path.Combine(environment.ContentRootPath, "Storage"));

        services.AddOptions<GotenbergOptions>()
            .Bind(configuration.GetSection("GOTENBERG"))
            .Validate(options =>
                Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                "GOTENBERG:BaseUrl must be a valid absolute URL.")
            .Validate(options => options.TimeoutInMinutes > 0,
                "GOTENBERG:TimeoutInMinutes must be greater than zero.")
            .Validate(options => options.MaxAttempts >= 1,
                "GOTENBERG:MaxAttempts must be at least one.")
            .Validate(options => options.RetryDelayInMinutes >= 0,
                "GOTENBERG:RetryDelayInMinutes cannot be negative.")
            .ValidateOnStart();

        return services;
    }
}

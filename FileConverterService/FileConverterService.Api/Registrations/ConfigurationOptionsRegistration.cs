using FileConverterService.Infrastructure.HelperServices.Storage;
using FileConverterService.Infrastructure.Converters;
using Microsoft.Extensions.Options;
using FileConverterService.Infrastructure.Queues;

namespace FileConverterService.Api.Registrations;

public static class ConfigurationOptionsRegistration
{
    public static IServiceCollection RegisterConfigurationOptions(
        this IServiceCollection services,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection("REDIS"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "REDIS:ConnectionString is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.QueueKey),
                "REDIS:QueueKey is required.")
            .Validate(options => options.PollIntervalMilliseconds > 0,
                "REDIS:PollIntervalMilliseconds must be greater than zero.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BatchKey),
                "REDIS:BatchKey is require.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.JobKey),
                "REDIS:JobKey is require.")
            .ValidateOnStart();

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

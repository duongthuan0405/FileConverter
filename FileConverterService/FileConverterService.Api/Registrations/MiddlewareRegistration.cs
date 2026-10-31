using FileConverterService.Api.Middlewares;

namespace FileConverterService.Api.Registrations;

public static class MiddlewareRegistration
{
    public static IServiceCollection RegisterMiddlewares(this IServiceCollection services)
    {
        services.AddTransient<GlobalExceptionHandlerMiddleware>();
        return services;
    }
}

using FileConverterService.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FileConverterService.Api.Registrations
{
    public static class UseCasesHandlersRegistration
    {
        public static IServiceCollection RegisterUseCasesHandlers(this IServiceCollection services)
        {
            var applicationAssembly = typeof(FileConverterService.Application.AssemblyReference).Assembly;

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(applicationAssembly);
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            });

            // Quét validator handler trong assembly
            services.AddValidatorsFromAssembly(applicationAssembly);

            return services;
        }
    }
}

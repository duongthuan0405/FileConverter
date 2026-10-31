using FileConverterService.Api.Middlewares;
using FileConverterService.Api.Registrations;
using DotNetEnv;

namespace FileConverterService.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Env.Load();
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.RegisterUseCasesHandlers();
            builder.Services.RegisterMiddlewares();
            builder.Services.RegisterHelperServices();
            builder.Services.RegisterWorkers();
            builder.Services.RegisterRepositories();
            builder.Services.RegisterConfigurationOptions(builder.Environment, builder.Configuration);

            var app = builder.Build();

            // Sử dụng Global Exception Handler Middleware ở đầu pipeline
            app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}

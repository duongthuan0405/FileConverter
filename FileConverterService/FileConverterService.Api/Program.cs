using FileConverterService.Api.Middlewares;
using FileConverterService.Api.Registrations;
using DotNetEnv;
using Serilog;

namespace FileConverterService.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Deployment environment variables take precedence over local .env values.
            Env.NoClobber().Load();
            var builder = WebApplication.CreateBuilder(args);

            // Resolve relative log paths against the application root, not the shell directory.
            const string filePathSetting = "Serilog:WriteTo:1:Args:path";
            var logPath = builder.Configuration[filePathSetting]
                ?? throw new InvalidOperationException("The log file path is required.");
            builder.Configuration[filePathSetting] = Path.GetFullPath(logPath, builder.Environment.ContentRootPath);
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();
            builder.Services.AddSerilog(Log.Logger, dispose: false);

            try
            {
                Log.Information("Starting FileConverterService");

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

                // Wrap the exception middleware so completion events see the final HTTP status.
                app.UseSerilogRequestLogging();

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
            catch (Exception exception)
            {
                Log.Fatal(exception, "FileConverterService terminated unexpectedly");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}

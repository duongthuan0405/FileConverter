using System.Net;
using System.Text.Json;
using FileConverterService.Api.ApiResponseModels;
using FileConverterService.Api.Constants;
using FileConverterService.Application.BusinessExceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FileConverterService.Api.Middlewares
{
    public class GlobalExceptionHandlerMiddleware : IMiddleware
    {
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

        public GlobalExceptionHandlerMiddleware(ILogger<GlobalExceptionHandlerMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var statusCode = exception switch
            {
                NotFoundException => HttpStatusCode.NotFound,
                UnauthorizedException => HttpStatusCode.Unauthorized,
                ForbiddenException => HttpStatusCode.Forbidden,
                ConflictUniqueValueException => HttpStatusCode.Conflict,
                BadRequestException => HttpStatusCode.BadRequest,
                _ => HttpStatusCode.InternalServerError
            };

            context.Response.StatusCode = (int)statusCode;

            ApiErrorResponse errorResponse;

            if (exception is BusinessException businessEx)
            {
                errorResponse = new ApiErrorResponse(
                    message: exception.Message.Length > 0 ? exception.Message : ApiErrorResponseMessage.DefaultMessage,
                    errors: businessEx.Errors
                );
            }
            else
            {
                errorResponse = new ApiErrorResponse(
                    message: exception.Message.Length > 0 ? exception.Message : ApiErrorResponseMessage.DefaultMessage
                );
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(errorResponse, jsonOptions);
            await context.Response.WriteAsync(json);
        }
    }
}

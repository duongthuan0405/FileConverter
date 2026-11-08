FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FileConverterService.Domain/FileConverterService.Domain.csproj FileConverterService.Domain/
COPY FileConverterService.Application/FileConverterService.Application.csproj FileConverterService.Application/
COPY FileConverterService.Infrastructure/FileConverterService.Infrastructure.csproj FileConverterService.Infrastructure/
COPY FileConverterService.Api/FileConverterService.Api.csproj FileConverterService.Api/
RUN dotnet restore FileConverterService.Api/FileConverterService.Api.csproj

COPY . .
RUN dotnet publish FileConverterService.Api/FileConverterService.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .
RUN mkdir -p /app/Storage/input /app/Storage/output /app/Logs \
    && chown -R app:app /app/Storage /app/Logs

# Mount persistent storage at /app/Storage and logs at /app/Logs.
# Supply Redis/Gotenberg/worker settings through environment variables at runtime.
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "FileConverterService.Api.dll"]

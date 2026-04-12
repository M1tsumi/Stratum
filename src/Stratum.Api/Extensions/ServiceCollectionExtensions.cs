namespace Stratum.Api.Extensions;

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Stratum.Api.Endpoints;
using Stratum.Api.Middleware;
using Stratum.Application.Interfaces;
using Stratum.Domain.Interfaces;
using Stratum.Domain.Services;
using Stratum.Infrastructure.Authentication;
using Stratum.Infrastructure.Metadata.SQLite;
using Stratum.Infrastructure.Storage.FileSystem;

/// <summary>
/// Extension methods for configuring the Stratum API services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds and configures Stratum API services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStratumApi(this IServiceCollection services, IConfiguration configuration)
    {
        // Add health checks
        services.AddHealthChecks();

        // Add CORS (configured for development)
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        // Add controllers (if needed for complex responses)
        services.AddControllers();

        // Add API explorer and Swagger for development
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Register infrastructure services
        services.AddSingleton<IMetadataStore>(sp => new SQLiteMetadataStore("Data Source=./data/stratum.db"));
        services.AddSingleton<IObjectStore>(sp => new FileSystemObjectStore("./data/objects"));
        services.AddSingleton<SigV4Validator>();
        services.AddSingleton<ETagCalculator>();

        // Configure Kestrel
        services.Configure<KestrelServerOptions>(options =>
        {
            var listenUrl = configuration["Server:ListenUrl"] ?? "http://0.0.0.0:9000";
            var enableHttp3 = configuration.GetValue<bool>("Server:EnableHttp3", true);
            var maxRequestBodySize = configuration.GetValue<long>("Server:MaxRequestBodySize", 5L * 1024 * 1024 * 1024);
            var maxRequestBufferSize = configuration.GetValue<int>("Server:MaxRequestBufferSize", 1024 * 1024);

            var uri = new Uri(listenUrl);
            options.ListenAnyIP(uri.Port, listenOptions =>
            {
                listenOptions.Protocols = enableHttp3 ? HttpProtocols.Http1AndHttp2AndHttp3 : HttpProtocols.Http1AndHttp2;
            });

            options.Limits.MaxRequestBodySize = maxRequestBodySize;
            options.Limits.MaxRequestBufferSize = maxRequestBufferSize;
            options.Limits.MaxConcurrentConnections = 10000;
            options.Limits.MaxConcurrentUpgradedConnections = 1000;
            options.AddServerHeader = false;
            options.AllowSynchronousIO = false;
        });

        return services;
    }
}

/// <summary>
/// Extension methods for configuring the Stratum API middleware pipeline.
/// </summary>
public static class ApplicationExtensions
{
    /// <summary>
    /// Configures the Stratum API middleware pipeline.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static IApplicationBuilder UseStratumApi(this IApplicationBuilder app)
    {
        // Use HTTPS redirection in production
        if (app is WebApplication webApp && webApp.Environment.IsProduction())
        {
            app.UseHttpsRedirection();
        }

        // Use CORS
        app.UseCors();

        // Use URL normalization for S3-style URLs
        app.UseUrlNormalization();

        // Use Swagger in development
        if (app is WebApplication webApp2 && webApp2.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }
}

/// <summary>
/// Extension methods for configuring the Stratum API endpoint routing.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Stratum API endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapStratumEndpoints(this WebApplication app)
    {
        // Use health checks endpoint
        app.MapHealthChecks("/health");

        // Map S3 API endpoints
        app.MapBucketEndpoints();
        app.MapObjectEndpoints();
        app.MapMultipartEndpoints();

        // Map root endpoint
        app.MapGet("/", () => Results.Ok(new
        {
            Service = "Stratum S3-Compatible Object Storage",
            Version = "0.1.0",
            Status = "Running"
        }));

        return app;
    }
}

using Serilog;
using Stratum.Api.Extensions;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddEnvironmentVariables()
        .Build())
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/stratum-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting Stratum API");
    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Add configuration
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    // Add services
    builder.Services.AddStratumApi(builder.Configuration);

    var app = builder.Build();

    // Configure middleware
    app.UseStratumApi();

    // Map endpoints
    app.MapStratumEndpoints();

    Log.Information("Stratum API started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Stratum API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

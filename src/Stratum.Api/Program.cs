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

    // Ensure data directory exists
    var dataDirectory = builder.Configuration["Storage:DataDirectory"] ?? "./data";
    if (!Directory.Exists(dataDirectory))
    {
        Log.Information("Creating data directory: {DataDirectory}", dataDirectory);
        Directory.CreateDirectory(dataDirectory);
    }
    else
    {
        Log.Information("Using existing data directory: {DataDirectory}", dataDirectory);
    }

    // Ensure objects subdirectory exists
    var objectsDirectory = Path.Combine(dataDirectory, "objects");
    if (!Directory.Exists(objectsDirectory))
    {
        Log.Information("Creating objects directory: {ObjectsDirectory}", objectsDirectory);
        Directory.CreateDirectory(objectsDirectory);
    }

    // Ensure logs directory exists
    var logsDirectory = "logs";
    if (!Directory.Exists(logsDirectory))
    {
        Log.Information("Creating logs directory: {LogsDirectory}", logsDirectory);
        Directory.CreateDirectory(logsDirectory);
    }

    // Use Serilog
    builder.Host.UseSerilog();

    // Add configuration
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    // Validate configuration
    var listenUrl = builder.Configuration["Server:ListenUrl"];
    if (string.IsNullOrEmpty(listenUrl))
    {
        Log.Warning("Server:ListenUrl not configured, using default: http://0.0.0.0:9000");
    }

    var dataDir = builder.Configuration["Storage:DataDirectory"];
    if (string.IsNullOrEmpty(dataDir))
    {
        Log.Warning("Storage:DataDirectory not configured, using default: ./data");
    }

    Log.Information("Configuration: ListenUrl={ListenUrl}, DataDirectory={DataDirectory}", 
        listenUrl ?? "http://0.0.0.0:9000", 
        dataDir ?? "./data");

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

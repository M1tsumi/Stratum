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

    // Print startup banner
    PrintStartupBanner(builder);

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

    // Validate SQLite connection string
    var dbPath = Path.Combine(dataDir ?? "./data", "stratum.db");
    var dbDirectory = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
    {
        Log.Information("Creating database directory: {DbDirectory}", dbDirectory);
        Directory.CreateDirectory(dbDirectory);
    }

    // Test SQLite connection
    try
    {
        Log.Information("Testing SQLite connection...");
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();
        Log.Information("SQLite connection test successful");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Failed to connect to SQLite database");
        throw;
    }

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

static void PrintStartupBanner(WebApplicationBuilder builder)
{
    var version = "0.1.0-alpha.1";
    var listenUrl = builder.Configuration["Server:ListenUrl"] ?? "http://0.0.0.0:9000";
    var dataDir = builder.Configuration["Storage:DataDirectory"] ?? "./data";
    var enableHttp3 = builder.Configuration.GetValue<bool>("Server:EnableHttp3", true);
    var environment = builder.Environment.EnvironmentName;

    Console.WriteLine();
    Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                                                               ║");
    Console.WriteLine("║   STRATUM - S3-Compatible Object Storage                      ║");
    Console.WriteLine("║                                                               ║");
    Console.WriteLine($"║   Version: {version,-52} ║");
    Console.WriteLine($"║   Environment: {environment,-46} ║");
    Console.WriteLine($"║   Listen URL: {listenUrl,-47} ║");
    Console.WriteLine($"║   Data Directory: {dataDir,-43} ║");
    Console.WriteLine($"║   HTTP/3: {(enableHttp3 ? "Enabled" : "Disabled"),-48} ║");
    Console.WriteLine("║                                                               ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
}

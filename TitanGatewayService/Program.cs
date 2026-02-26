using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using TitanGatewayService;
using TitanGatewayService.Devices.Miranda;
using TitanGatewayService.Devices.Oberon;
using TitanGatewayService.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Determine environment and log directory
var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
var logDirKey = env == "Development" ? "Logging:LogDirectory" : "Logging:ProductionLogDirectory";
var logDirectory = builder.Configuration[logDirKey] ?? "Logs";

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));

var fullLogPath = Path.IsPathRooted(logDirectory)
    ? logDirectory
    : Path.Combine(repoRoot, logDirectory);

Directory.CreateDirectory(fullLogPath);

Log.Logger = new LoggerConfiguration()
    // Suppress noisy framework HTTP client info logs
    .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        theme: SystemConsoleTheme.Colored)
    .WriteTo.File(
        Path.Combine(fullLogPath, "TitanGateway-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .CreateLogger();


// Replace default logger and add Serilog
builder.Logging.ClearProviders();        // remove default console logger
builder.Logging.AddSerilog(Log.Logger);  // use Serilog instead

// Register resilient HTTP clients with built-in retry policies for external services and devices
builder.Services.AddResilientHttpClients();

// Register other application services
builder.Services
    .AddOptions<MirandaScheduleOptions>()
    .Bind(builder.Configuration.GetSection("MirandaSchedule"));

builder.Services
    .AddOptions<OberonScheduleOptions>()
    .Bind(builder.Configuration.GetSection("OberonSchedule"));

builder.Services.AddSingleton<IDeviceConfigurationManager, DeviceConfigurationManager>();
builder.Services.AddSingleton<IDeviceFactory, DeviceFactory>();
builder.Services.AddSingleton<DeviceManager>();
builder.Services.AddHostedService<Worker>();

Log.Information("Starting Titan Gateway Service. Please Stand by ....");

var host = builder.Build();
host.Run();

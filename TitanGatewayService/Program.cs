using TitanGatewayService;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;


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
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        theme: SystemConsoleTheme.Colored)
    .WriteTo.File(
        Path.Combine(fullLogPath, "TitanGateway-.log"),
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

// Replace default logger and add Serilog
builder.Logging.ClearProviders();        // remove default console logger
builder.Logging.AddSerilog(Log.Logger);  // use Serilog instead

builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<IDeviceConfigurationManager, DeviceConfigurationManager>();


Log.Information("Starting Titan Gateway Service. Please Stand by ....");

var host = builder.Build();
host.Run();

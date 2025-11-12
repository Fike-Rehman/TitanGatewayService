using TitanGatewayService;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;


var builder = Host.CreateApplicationBuilder(args);

var logDirectory = builder.Configuration["Logging:LogDirectory"] ?? "Logs";
Directory.CreateDirectory(logDirectory);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        theme: SystemConsoleTheme.Colored)
    .WriteTo.File(
        Path.Combine(logDirectory, "gateway-.log"),
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

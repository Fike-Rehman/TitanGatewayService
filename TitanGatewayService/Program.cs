using TitanGatewayService;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// 1. Configure Serilog
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console() // This extension method is provided by Serilog.Sinks.Console
    .WriteTo.File("logs/gateway-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

// 2. Replace default logger
builder.Logging.ClearProviders();        // remove default console logger
builder.Logging.AddSerilog(Log.Logger);  // use Serilog instead


builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<IDeviceConfigurationManager, DeviceConfigurationManager>();

var host = builder.Build();
host.Run();

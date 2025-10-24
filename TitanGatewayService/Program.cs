using TitanGatewayService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<IDeviceConfigurationManager, DeviceConfigurationManager>();


var host = builder.Build();
host.Run();

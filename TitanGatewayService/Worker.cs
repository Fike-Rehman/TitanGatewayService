using TitanGatewayService.Devices;

namespace TitanGatewayService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly List<IDeviceClient> _devices = new();

        public Worker(ILogger<Worker> logger, IConfiguration config)
        {
            _logger = logger;

            foreach (var deviceConfig in config.GetSection("Devices").GetChildren())
            {
                var type = deviceConfig["Type"];
                var name = deviceConfig["Name"];
                var location = deviceConfig["Location"];
                var baseUrl = deviceConfig["BaseUrl"];

                IDeviceClient device = type switch
                {
                    "Oberon" => new OberonClient(name, location, baseUrl),
                    "Miranda" => new MirandaClient(name, location, baseUrl),
                    _ => throw new NotSupportedException($"Unknown device type {type}")
                };

                _devices.Add(device);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var device in _devices)
                {
                    if (await device.PingAsync())
                    {
                        _logger.LogInformation($"{device.Name} is online");

                    }
                    else
                    {
                        _logger.LogWarning($"{device.Name} is offline");
                    }

                    _logger.LogWarning($"{device.Name} is offline");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

}

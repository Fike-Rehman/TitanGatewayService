using CTS.Utilities.Common;
using TitanGatewayService.Devices;

namespace TitanGatewayService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly List<IDeviceClient> _devices = new();
        private readonly IDeviceConfigurationManager _deviceManager;
        private readonly SolarApiClient _solarApiClient;

        public Worker(ILogger<Worker> logger, IDeviceConfigurationManager deviceManager, SolarApiClient solarApiClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _solarApiClient = solarApiClient ?? throw new ArgumentNullException(nameof(solarApiClient));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _devices.AddRange(_deviceManager.GetAllDevices());  
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var (sunrise, sunset) = await _solarApiClient.GetSolarTimesAsync();

            _logger.LogInformation("Sunrise at: {Sunrise}, Sunset at: {Sunset}", sunrise, sunset);
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var device in _devices)
                {
                    if (await device.PingAsync() == "OK")
                    {
                        _logger.LogInformation("{DeviceName}, Location: {DeviceLocation} is online", device.Name, device.Location);
                    }
                    else
                    {
                        _logger.LogWarning("{DeviceName} is offline", device.Name);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}

using TitanGatewayService.Devices;

namespace TitanGatewayService
{
    public class DeviceConfigurationManager : IDeviceConfigurationManager
    {
        private readonly List<IDeviceClient> _devices = new();

        public DeviceConfigurationManager(IConfiguration config)
        {
            var deviceSection = config.GetSection("Devices").GetChildren();

            foreach (var deviceConfig in deviceSection)
            {
                var type = deviceConfig["Type"];
                var name = deviceConfig["Name"];
                var location = deviceConfig["Location"];
                var baseUrl = deviceConfig["BaseUrl"];

                var device = DeviceFactory.Create(type, name, location, baseUrl);
                _devices.Add(device);
            }
        }

        public IReadOnlyCollection<IDeviceClient> GetAllDevices() => _devices.AsReadOnly();
    }
}

using TitanGatewayService.Devices;

namespace TitanGatewayService
{
    public class DeviceManager
    {
        private readonly List<IDeviceClient> _devices = new();

        public IReadOnlyCollection<IDeviceClient> Devices => _devices;

        public DeviceManager(
            IDeviceConfigurationManager configManager,
            IDeviceFactory deviceFactory)
        {
            foreach (var deviceConfig in configManager.GetAllDevices())
            {
                var device = deviceFactory.Create(deviceConfig);
                _devices.Add(device);
            }
        }
    }
}

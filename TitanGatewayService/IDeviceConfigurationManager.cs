using TitanGatewayService.Devices;

namespace TitanGatewayService
{
    public interface IDeviceConfigurationManager
    {
        IReadOnlyList<DeviceConfig> GetAllDevices();
    }
}

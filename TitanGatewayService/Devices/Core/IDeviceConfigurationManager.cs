
namespace TitanGatewayService.Devices.Core
{
    public interface IDeviceConfigurationManager
    {
        IReadOnlyList<DeviceConfig> GetAllDevices();
    }
}

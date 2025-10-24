using TitanGatewayService.Devices;

namespace TitanGatewayService
{
    public interface IDeviceConfigurationManager
    {
        IReadOnlyCollection<IDeviceClient> GetAllDevices();
    }
}

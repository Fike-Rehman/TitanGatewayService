namespace TitanGatewayService
{
    public class DeviceConfigurationManager : IDeviceConfigurationManager
    {
        private readonly IConfiguration _configuration;

        public DeviceConfigurationManager(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IReadOnlyList<DeviceConfig> GetAllDevices()
        {
            return _configuration
                .GetSection("Devices")
                .Get<List<DeviceConfig>>() ?? new();
        }
    }
}

using TitanGatewayService.Devices;

namespace TitanGatewayService
{

    public class DeviceFactory : IDeviceFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public DeviceFactory(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public IDeviceClient Create(DeviceConfig config)
        {
            return config.Type switch
            {
                "Miranda" => new MirandaClient(
                    _httpClientFactory.CreateClient(),
                    config.Name,
                    config.Location,
                    config.BaseUrl),

                "Oberon" => new OberonClient(
                _httpClientFactory.CreateClient(),
                config.Name,
                config.Location,
                config.BaseUrl),

                _ => throw new NotSupportedException(
                        $"Unknown device type {config.Type}")
            };
        }
    }
}

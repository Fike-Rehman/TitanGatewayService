using TitanGatewayService.Devices;

namespace TitanGatewayService
{
    public static class DeviceFactory
    {
        public static IDeviceClient Create(string type, string name, string location, string baseUrl)
        {
            return type switch
            {
                "Oberon" => new OberonClient(name, location, baseUrl),
                "Miranda" => new MirandaClient(name, location, baseUrl),
                _ => throw new NotSupportedException($"Unknown device type: {type}")
            };
        }
    }
}

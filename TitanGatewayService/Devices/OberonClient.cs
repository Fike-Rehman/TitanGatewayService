using static System.Net.WebRequestMethods;

namespace TitanGatewayService.Devices
{
    public class OberonClient : IDeviceClient
    {
        public string Name { get; }

        public string BaseUrl { get; }

        public string Location { get; }

        public OberonClient(string name, string location, string baseUrl)
        {
            Name = name;
            Location = location;
            BaseUrl = baseUrl;
        }

        public async Task<bool> PingAsync()
        {
            // TODO: Implement actual ping logic
            return true;
        }
    }
}

namespace TitanGatewayService.Devices
{
    public class MirandaClient : IDeviceClient
    {
        public string Name { get; }

        public string BaseUrl { get; }

        public string Location { get; }

        public MirandaClient(string name, string location, string baseUrl)
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

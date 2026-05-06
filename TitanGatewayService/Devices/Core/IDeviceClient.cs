namespace TitanGatewayService.Devices.Core
{
    public interface IDeviceClient
    {
        string Name { get; }

        string BaseUrl { get; }

        string Location { get; }

        Task<string> PingAsync();
    }
}

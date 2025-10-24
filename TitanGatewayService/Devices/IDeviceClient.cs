namespace TitanGatewayService.Devices
{
    public interface IDeviceClient
    {
        string Name { get; }
        string BaseUrl { get; }

        string Location { get; }

        Task<bool> PingAsync();
    }
}

namespace TitanGatewayService.Devices.Core
{
    public interface ISwitchDevice : IDeviceClient
    {
        Task<string> TurnOnAsync(string? switchId = null, CancellationToken cancellationToken = default);

        Task<string> TurnOffAsync(string? switchId = null, CancellationToken cancellationToken = default);
    }
}

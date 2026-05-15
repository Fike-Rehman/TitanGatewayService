using System.Net.Http.Headers;
using TitanGatewayService.Devices.Core;

namespace TitanGatewayService.Devices.Miranda
{
    public class MirandaClient : ISwitchDevice
    {
        private readonly HttpClient _httpClient;

        public string Name { get; }
        public string Location { get; }
        public string BaseUrl { get; }

        public MirandaClient(
            HttpClient httpClient,
            string name,
            string location,
            string baseUrl)
        {
            _httpClient = httpClient;
            Name = name;
            Location = location;
            BaseUrl = baseUrl;

            _httpClient.BaseAddress = new Uri(BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("text/plain"));
        }

        public async Task<string> PingAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("ping");

                return response.IsSuccessStatusCode
                    ? "OK"
                    : $"HTTP {(int)response.StatusCode}";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> TurnOnAsync(string? switchId = null, CancellationToken cancellationToken = default)
            => await SendSwitchCommandAsync("on", switchId, cancellationToken);

        public async Task<string> TurnOffAsync(string? switchId = null, CancellationToken cancellationToken = default)
            => await SendSwitchCommandAsync("off", switchId, cancellationToken);

        private async Task<string> SendSwitchCommandAsync(string action, string? switchId, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(switchId))
                {
                    return "SwitchId is required for Miranda switch commands.";
                }

                var endpoint = $"{switchId}/{action}";
                var response = await _httpClient.GetAsync(endpoint, cancellationToken);

                return response.IsSuccessStatusCode
                    ? "OK"
                    : $"HTTP {(int)response.StatusCode}";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}

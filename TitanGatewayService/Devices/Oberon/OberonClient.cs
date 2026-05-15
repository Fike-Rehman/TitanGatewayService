using System.Net.Http.Headers;
using TitanGatewayService.Devices.Core;

namespace TitanGatewayService.Devices.Oberon
{
    public class OberonClient : ISwitchDevice
    {
        private readonly HttpClient _httpClient;

        public string Name { get; }
        public string BaseUrl { get; }
        public string Location { get; }

        public OberonClient(
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
            => await SendSwitchCommandAsync("on", cancellationToken);

        public async Task<string> TurnOffAsync(string? switchId = null, CancellationToken cancellationToken = default)
            => await SendSwitchCommandAsync("off", cancellationToken);

        private async Task<string> SendSwitchCommandAsync(string action, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync(action, cancellationToken);

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

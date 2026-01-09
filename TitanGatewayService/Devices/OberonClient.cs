using System.Net.Http.Headers;
using System.Runtime;

namespace TitanGatewayService.Devices
{
    public class OberonClient : IDeviceClient
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
    }
}

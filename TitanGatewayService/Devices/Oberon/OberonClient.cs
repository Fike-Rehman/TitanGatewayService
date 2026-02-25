using System.Net.Http.Headers;
using System.Runtime;

namespace TitanGatewayService.Devices.Oberon
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
    }
}

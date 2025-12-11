using CTS.Utilities.Common;

namespace TitanGatewayService
{
    public class SolarApiClient
    {
        private readonly HttpClient _httpClient;

        public SolarApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            _httpClient.BaseAddress = new Uri("https://api.sunrise-sunset.org/");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<SolarResults> GetSolarTimesAsync(double latitude, double longitude)
        {
            //var baseUrl = _httpClient.BaseAddress.ToString();
            var solarData = await SolarService.GetSolarTimesAsync(_httpClient);

            return new SolarResults
            {
                Sunrise = solarData.sunrise,
                Sunset = solarData.sunset
            };
        }
    }
}

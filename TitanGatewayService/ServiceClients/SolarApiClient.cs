using CTS.Utilities.Common;
using Newtonsoft.Json;

namespace TitanGatewayService
{
    public class SolarApiClient
    {
        // Minneapolis coordinates
        private const double latitude = 44.9799700;
        private const double longitude = -93.2638400;

        private readonly HttpClient _httpClient;

        public SolarApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            _httpClient.BaseAddress = new Uri("https://api.sunrise-sunset.org/");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<(DateTime sunrise, DateTime sunset)> GetSolarTimesAsync()
        {
            var url = $"json?lat={latitude}&lng={longitude}&formatted=0";

            try
            {
                using var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    throw new Exception("API request failed");

                var json = await response.Content.ReadAsStringAsync();

                var solarData = JsonConvert.DeserializeObject<SolarResponse>(json);

                if (solarData is not { Results: not null })
                    throw new Exception("Invalid response from solar API.");


                if (!"OK".Equals(solarData.Status, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Solar API returned non-OK status.");

                // Convert from UTC
                var sunriseLocal = solarData.Results.Sunrise.ToLocalTime();
                var sunsetLocal = solarData.Results.Sunset.ToLocalTime();

                // If sunset is for previous day, bump it to next one
                if (sunsetLocal.Date < DateTime.Now.Date)
                {
                    sunsetLocal = sunsetLocal.AddDays(1);
                }

                return (sunriseLocal, sunsetLocal);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to retrieve sunrise/sunset times", ex);
            }
        }
    }
}

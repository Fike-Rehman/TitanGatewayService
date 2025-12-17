using CTS.Utilities.Common;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using TitanGatewayService.Extensions;

namespace TitanGatewayService
{
    public class SolarApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<SolarApiClientOptions> _options;

        public SolarApiClient(
            HttpClient httpClient,
            IOptions<SolarApiClientOptions> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            _httpClient.BaseAddress = new Uri(_options.Value.BaseUrl);
            _httpClient.Timeout = _options.Value.TimeoutSeconds;
        }

        public async Task<(DateTime sunrise, DateTime sunset)> GetSolarTimesAsync()
        {
            var url = $"json?lat={_options.Value.Latitude}&lng={_options.Value.Longitude}&formatted=0";

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

using Newtonsoft.Json;

namespace CTS.Utilities.Common
{
    /// <summary>
    /// Provides Sunset and sunrise times for the given location coordinates. If Location
    /// coordinate properties are not set prior to calling the GetSunTimes methods, Sunrise
    /// and Sunset times for Minneapolis are returned by default. The class utilizes
    /// http://sunrise-sunset.org/api . 
    /// </summary>
    public static class SolarService
    {
        // By default we use Minneapolis coordinates
      //  public static double Latitude { get; set; } = 44.9799700;

      //  public static double Longitude { get; set; } = -93.2638400;

        public static async Task<(DateTime sunrise, DateTime sunset)> GetSolarTimesAsync(HttpClient httpClient)
        {
            
           // var url = $"{baseUrl}json?lat={latitude}&lng={longitude}&formatted=0";

            var url = $"https://api.sunrise-sunset.org/json?lat=44.9799700&lng=-93.2638400&formatted=0";
            try
            {
                using var response = await  httpClient.GetAsync(url);

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
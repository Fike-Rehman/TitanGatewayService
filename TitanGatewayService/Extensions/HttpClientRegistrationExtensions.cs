using Polly;
using Polly.Extensions.Http;

namespace TitanGatewayService.Extensions
{
    public static class HttpClientRegistrationExtensions
    {
        public static IServiceCollection AddResilientHttpClients(this IServiceCollection services)
        {
            services.AddSolarApi(
                services.BuildServiceProvider()
                        .GetRequiredService<IConfiguration>());

            // Add other resilient HTTP clients here as needed

            return services;
        }

        public static IServiceCollection AddSolarApi(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Bind options for this API
            services.AddOptions<SolarApiClientOptions>()
                .Bind(configuration.GetSection("SolarServiceApi"))
                .Validate(o => o.Latitude != 0 && o.Longitude != 0,
                  "Latitude and Longitude must be set")
                .Validate(o => Uri.IsWellFormedUriString(o.BaseUrl, UriKind.Absolute),
                  "BaseUrl must be a valid absolute URL")
                .Validate(o => o.TimeoutSeconds.TotalSeconds > 0,
                  "TimeoutSeconds must be greater than zero")
                .ValidateOnStart();

            // Retry policy: 3 retries, exponential backoff
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(3, attempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt)));

            // Register typed HttpClient
            services.AddHttpClient<SolarApiClient>()
                    .AddPolicyHandler(retryPolicy);

            return services;
        }
    }   
}

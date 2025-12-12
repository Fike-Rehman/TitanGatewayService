using Polly;
using Polly.Extensions.Http;

namespace TitanGatewayService.Extensions
{
    public static class HttpClientRegistrationExtensions
    {
        public static IServiceCollection AddResilientHttpClients(this IServiceCollection services)
        {
            // Retry policy: 3 retries, exponential backoff
            var retryPolicySolarApi = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(3, attempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt)));

            // TODO: Different policies for different clients can be defined here

            services.AddHttpClient<SolarApiClient>()
                    .AddPolicyHandler(retryPolicySolarApi);

            // Add more typed device clients here later:
            //
            // services.AddHttpClient<SwitchDeviceClient>().AddPolicyHandler(retryPolicy);
            // services.AddHttpClient<LightDeviceClient>().AddPolicyHandler(retryPolicy);
            // services.AddHttpClient<ThermostatClient>().AddPolicyHandler(retryPolicy);

            return services;
        }
    }
    
}

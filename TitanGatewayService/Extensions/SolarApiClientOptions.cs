namespace TitanGatewayService.Extensions
{
    public sealed class SolarApiClientOptions
    {
        public string BaseUrl { get; set; } = String.Empty;
        public double Latitude { get; set; } = 0.0;
        public double Longitude { get; set; } = 0.0;
        public TimeSpan TimeoutSeconds { get; set; } = TimeSpan.FromSeconds(10);
    }
}

namespace CTS.Utilities.Common
{
    public class SolarResponse
    {
        public string? Status { get; set; }
        public SolarResults? Results { get; set; }

    }

    public class SolarResults
    {
        public DateTime Sunrise { get; set; }
        public DateTime Sunset { get; set; }
    }
}

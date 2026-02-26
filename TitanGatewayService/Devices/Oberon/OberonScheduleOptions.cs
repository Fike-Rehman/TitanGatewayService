namespace TitanGatewayService.Devices.Oberon
{
    public sealed class OberonScheduleOptions
    {
        public OberonRetryOptions Retry { get; set; } = new();
        public List<OberonDeviceSchedule> Devices { get; set; } = new();
    }

    public sealed class OberonRetryOptions
    {
        public int MaxAttempts { get; set; } = 5;
        public int IntervalSeconds { get; set; } = 60;
    }

    public sealed class OberonDeviceSchedule
    {
        public string DeviceName { get; set; } = string.Empty;
        // Ordered events for the single switch
        public List<OberonScheduledEvent> Events { get; set; } = new();
    }

    public sealed class OberonScheduledEvent
    {
        // "On" or "Off"
        public string Action { get; set; } = "On";

        // Type of schedule for this event (DailyTime | SolarOffset)
        public string Type { get; set; } = "DailyTime";

        // Used when Type = DailyTime (example: "18:30:00")
        public TimeSpan? Time { get; set; }

        // Used when Type = SolarOffset (Sunrise | Sunset)
        public string SolarEvent { get; set; } = "Sunset";

        // Used when Type = SolarOffset (can be negative)
        public int OffsetMinutes { get; set; } = 0;
    }
}

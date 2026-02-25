namespace TitanGatewayService
{
    public sealed class MirandaScheduleOptions
    {
        public MirandaRetryOptions Retry { get; set; } = new();
        public List<MirandaSwitchSchedule> Switches { get; set; } = new();
    }

    public sealed class MirandaRetryOptions
    {
        public int MaxAttempts { get; set; } = 5;
        public int IntervalSeconds { get; set; } = 60;
    }

    public sealed class MirandaSwitchSchedule
    {
        public string DeviceName { get; set; } = string.Empty;
        public string SwitchId { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public MirandaActionSchedule On { get; set; } = new();
        public MirandaActionSchedule Off { get; set; } = new();
    }

    public sealed class MirandaActionSchedule
    {
        // DailyTime | SolarOffset
        public string Type { get; set; } = "DailyTime";

        // Used when Type = DailyTime (example: "18:30:00")
        public TimeSpan? Time { get; set; }

        // Used when Type = SolarOffset (Sunrise | Sunset)
        public string? SolarEvent { get; set; }

        // Used when Type = SolarOffset (can be negative)
        public int OffsetMinutes { get; set; } = 0;
    }
}

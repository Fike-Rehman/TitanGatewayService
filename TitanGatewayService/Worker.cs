using Microsoft.Extensions.Options;
using TitanGatewayService.Devices.Miranda;

namespace TitanGatewayService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly DeviceManager _deviceManager;
        private readonly SolarApiClient _solarApiClient;
        private readonly MirandaScheduleOptions _mirandaSchedule;

        public Worker(
            ILogger<Worker> logger, 
            DeviceManager deviceManager, 
            SolarApiClient solarApiClient,
            IOptions<MirandaScheduleOptions> mirandaScheduleOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _solarApiClient = solarApiClient ?? throw new ArgumentNullException(nameof(solarApiClient));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _mirandaSchedule = mirandaScheduleOptions?.Value ?? new MirandaScheduleOptions();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var (sunrise, sunset) = await _solarApiClient.GetSolarTimesAsync();

            _logger.LogInformation("");
            _logger.LogInformation("Today's Sunrise at: {Sunrise}, Sunset at: {Sunset}", sunrise, sunset);

            // Print configured devices and their On/Off schedules once at startup
            PrintConfiguredDevicesAndSchedules();

            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var device in _deviceManager.Devices)
                {
                    if (await device.PingAsync() == "OK")
                    {
                        _logger.LogInformation("{DeviceName}, Location: {DeviceLocation} is online", device.Name, device.Location);
                    }
                    else
                    {
                        _logger.LogWarning("{DeviceName} is offline", device.Name);
                    }
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected when the host is shutting down (e.g. Ctrl+C).
                    break;
                }
            }
        }

        private void PrintConfiguredDevicesAndSchedules()
        { 
            _logger.LogInformation("");
            _logger.LogInformation("----------- Configured devices and schedules -------------");
            _logger.LogInformation("");

            // Build a lookup for Miranda schedules by DeviceName for quick matching
            var scheduleLookup = _mirandaSchedule.Switches
                .GroupBy(s => s.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var device in _deviceManager.Devices)
            {
                if (scheduleLookup.TryGetValue(device.Name, out var schedules) && schedules.Any())
                {
                    foreach (var sched in schedules)
                    {
                        // Log each scheduled event in order
                        if (sched.Events == null || !sched.Events.Any())
                        {
                            _logger.LogInformation("{DeviceName} -- SwitchId: {SwitchId} has no events configured",
                                device.Name, sched.SwitchId);
                            continue;
                        }

                        foreach (var ev in sched.Events)
                        {
                            var evDesc = DescribeScheduledEvent(ev);
                            _logger.LogInformation("{DeviceName} -- Location: {DeviceLocation} - SwitchId: {SwitchId} {Action}: {Event}",
                                device.Name, device.Location, sched.SwitchId, ev.Action, evDesc);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("{DeviceName} Location: {DeviceLocation} - No schedule configured",
                        device.Name, device.Location);
                }
            }
        }

        private static string DescribeScheduledEvent(Devices.Miranda.MirandaScheduledEvent ev)
        {
            if (ev is null) return "None";

            return ev.Type switch
            {
                "DailyTime" => ev.Time.HasValue ? $"{ev.Time.Value:hh\\:mm\\:ss}" : "(time not set)",
                "SolarOffset" => $"{ev.SolarEvent} offset {ev.OffsetMinutes} minutes",
                _ => $"Unknown Type: {ev.Type}"
            };
        }
    }
}
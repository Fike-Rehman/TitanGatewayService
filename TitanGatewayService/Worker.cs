using Microsoft.Extensions.Options;
using TitanGatewayService.Devices.Miranda;
using TitanGatewayService.Devices.Oberon;

namespace TitanGatewayService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly DeviceManager _deviceManager;
        private readonly SolarApiClient _solarApiClient;
        private readonly MirandaScheduleOptions _mirandaSchedule;
        private readonly OberonScheduleOptions _oberonSchedule;

        public Worker(
            ILogger<Worker> logger, 
            DeviceManager deviceManager, 
            SolarApiClient solarApiClient,
            IOptions<MirandaScheduleOptions> mirandaScheduleOptions,
            IOptions<OberonScheduleOptions> oberonScheduleOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _solarApiClient = solarApiClient ?? throw new ArgumentNullException(nameof(solarApiClient));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _mirandaSchedule = mirandaScheduleOptions?.Value ?? new MirandaScheduleOptions();
            _oberonSchedule = oberonScheduleOptions?.Value ?? new OberonScheduleOptions();
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
            var mirandaScheduleLookup = _mirandaSchedule.Switches
                .GroupBy(s => s.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Build a lookup for Oberon schedules by DeviceName
            var oberonScheduleLookup = _oberonSchedule.Devices
                .GroupBy(d => d.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var device in _deviceManager.Devices)
            {
                var handled = false;

                if (mirandaScheduleLookup.TryGetValue(device.Name, out var schedules) && schedules.Any())
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
                    handled = true;
                }
                else if (oberonScheduleLookup.TryGetValue(device.Name, out var oberonSchedules) && oberonSchedules.Any())
                {
                    // Oberon devices normally have a single schedule entry per device, but handle multiple entries if present
                    foreach (var os in oberonSchedules)
                    {
                        if (os.Events == null || !os.Events.Any())
                        {
                            _logger.LogInformation("{DeviceName} -- has no events configured",
                                device.Name);
                            continue;
                        }

                        foreach (var ev in os.Events)
                        {
                            var evDesc = DescribeScheduledEvent(ev);
                            _logger.LogInformation("{DeviceName} -- Location: {DeviceLocation} - {Action}: {Event}",
                                device.Name, device.Location, ev.Action, evDesc);
                        }
                    }

                    handled = true;
                }
                
                if (!handled)
                {
                    _logger.LogInformation("{DeviceName} Location: {DeviceLocation} - No schedule configured",
                        device.Name, device.Location);
                }
            }
        }

        private static string DescribeScheduledEvent(MirandaScheduledEvent ev)
        {
            if (ev is null) return "None";

            return ev.Type switch
            {
                "DailyTime" => ev.Time.HasValue ? $"{ev.Time.Value:hh\\:mm\\:ss}" : "(time not set)",
                "SolarOffset" => $"{ev.SolarEvent} offset {ev.OffsetMinutes} minutes",
                _ => $"Unknown Type: {ev.Type}"
            };
        }

        private static string DescribeScheduledEvent(OberonScheduledEvent ev)
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
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using TitanGatewayService.Devices.Core;
using TitanGatewayService.Devices.Miranda;
using TitanGatewayService.Devices.Oberon;

namespace TitanGatewayService.Scheduling
{
    public sealed class ScheduleExecutionService : BackgroundService
    {
        // To avoid hitting the solar API at exactly midnight and to allow for some buffer in case of slight delays in service startup,
        // the schedule is refreshed at 12:05 AM each day.
        private static readonly TimeSpan DailySolarRefreshTime = TimeSpan.FromMinutes(5);

        private readonly ILogger<ScheduleExecutionService> _logger;
        private readonly DeviceManager _deviceManager;
        private readonly SolarApiClient _solarApiClient;
        private readonly MirandaScheduleOptions _mirandaSchedule;
        private readonly OberonScheduleOptions _oberonSchedule;
        private readonly string _solarCacheFilePath;
        private readonly HashSet<string> _executedEventKeys = new(StringComparer.OrdinalIgnoreCase);

        public ScheduleExecutionService(
            ILogger<ScheduleExecutionService> logger,
            DeviceManager deviceManager,
            SolarApiClient solarApiClient,
            IOptions<MirandaScheduleOptions> mirandaScheduleOptions,
            IOptions<OberonScheduleOptions> oberonScheduleOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _solarApiClient = solarApiClient ?? throw new ArgumentNullException(nameof(solarApiClient));
            _mirandaSchedule = mirandaScheduleOptions?.Value ?? new MirandaScheduleOptions();
            _oberonSchedule = oberonScheduleOptions?.Value ?? new OberonScheduleOptions();
            _solarCacheFilePath = Path.Combine(AppContext.BaseDirectory, "solar-times-cache.json");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var scheduleDate = DateTime.Now.Date;
                    _executedEventKeys.Clear();

                    var solarTimes = await GetSolarTimesForScheduleAsync(scheduleDate, stoppingToken);
                    
                    var todaysEvents = BuildDailySchedule(scheduleDate, solarTimes)
                        .OrderBy(e => e.ScheduledAt)
                        .ToList();

                    // Execute any events that were missed between the last schedule refresh and now, to ensure we end up in the correct state for the current time.
                    await CatchUpMissedEventsAsync(todaysEvents, DateTime.Now, stoppingToken);

                    // This blocks until tomorrow's refresh time, so the schedule is not rebuilt repeatedly.
                    await ExecuteRemainingScheduleForTodayAsync(todaysEvents, scheduleDate, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while executing scheduled actions. Retrying scheduler loop in one minute.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task ExecuteRemainingScheduleForTodayAsync(List<ScheduledSwitchEvent> todaysEvents, DateTime scheduleDate, CancellationToken cancellationToken)
        {
            var nextRefreshAt = scheduleDate.AddDays(1).Add(DailySolarRefreshTime);

            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                if (now >= nextRefreshAt)
                {
                    _logger.LogInformation("Refreshing solar schedule for the new day at {RefreshAt}.", now);
                    return;
                }

                var nextEvent = todaysEvents
                    .Where(e => e.ScheduledAt > now && !_executedEventKeys.Contains(e.EventKey))
                    .OrderBy(e => e.ScheduledAt)
                    .FirstOrDefault();

                var nextWakeUp = nextEvent is null || nextEvent.ScheduledAt > nextRefreshAt
                    ? nextRefreshAt
                    : nextEvent.ScheduledAt;

                // await DelayUntilAsync(nextWakeUp, cancellationToken);
                var delay = nextWakeUp - DateTime.Now;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }

                now = DateTime.Now;
                foreach (var dueEvent in todaysEvents.Where(e => e.ScheduledAt <= now && !_executedEventKeys.Contains(e.EventKey)).OrderBy(e => e.ScheduledAt))
                {
                    await ExecuteScheduledEventAsync(dueEvent, cancellationToken);
                }
            }
        }

        private async Task CatchUpMissedEventsAsync(List<ScheduledSwitchEvent> todaysEvents, DateTime now, CancellationToken cancellationToken)
        {
            var latestMissedEventsBySwitch = todaysEvents
                .Where(e => e.ScheduledAt <= now)
                .GroupBy(e => e.TargetKey, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(e => e.ScheduledAt).First())
                .OrderBy(e => e.ScheduledAt)
                .ToList();

            if (latestMissedEventsBySwitch.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Catching up {EventCount} missed scheduled events for {ScheduleDate}.", latestMissedEventsBySwitch.Count, now.Date);

            foreach (var missedEvent in latestMissedEventsBySwitch)
            {
                await ExecuteScheduledEventAsync(missedEvent, cancellationToken);
            }

            foreach (var pastEvent in todaysEvents.Where(e => e.ScheduledAt <= now))
            {
                _executedEventKeys.Add(pastEvent.EventKey);
            }
        }

        private async Task ExecuteScheduledEventAsync(ScheduledSwitchEvent scheduledEvent, CancellationToken cancellationToken)
        {
            if (!_executedEventKeys.Add(scheduledEvent.EventKey))
            {
                return;
            }

            var ping = await scheduledEvent.Device.PingAsync();
            if (!string.Equals(ping, "OK", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Skipping scheduled action {Action} for {DeviceName} (SwitchId: {SwitchId}). Device is offline: {PingStatus}", scheduledEvent.Action, scheduledEvent.Device.Name, scheduledEvent.SwitchId ?? "N/A", ping);
                return;
            }

            var result = scheduledEvent.Action.Equals("On", StringComparison.OrdinalIgnoreCase)
                ? await scheduledEvent.Device.TurnOnAsync(scheduledEvent.SwitchId, cancellationToken)
                : await scheduledEvent.Device.TurnOffAsync(scheduledEvent.SwitchId, cancellationToken);

            _logger.LogInformation("Executed scheduled action {Action} for {DeviceName} (SwitchId: {SwitchId}) at {ScheduledAt}. Result: {Result}", scheduledEvent.Action, scheduledEvent.Device.Name, scheduledEvent.SwitchId ?? "N/A", scheduledEvent.ScheduledAt, result);
        }

        private async Task<SolarTimes> GetSolarTimesForScheduleAsync(DateTime scheduleDate, CancellationToken cancellationToken)
        {
            try
            {
                var (sunrise, sunset) = await _solarApiClient.GetSolarTimesAsync(scheduleDate, cancellationToken);
                var solarTimes = new SolarTimes
                {
                    ScheduleDate = scheduleDate,
                    Sunrise = sunrise,
                    Sunset = sunset
                };

                SaveSolarTimesToCache(solarTimes);
                _logger.LogInformation("Loaded solar times for {ScheduleDate}. Sunrise: {Sunrise}, Sunset: {Sunset}", scheduleDate, sunrise, sunset);

                return solarTimes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve solar times for {ScheduleDate}. Attempting to use last known cached sunrise/sunset times.", scheduleDate);

                var cachedSolarTimes = LoadSolarTimesFromCache();
                if (cachedSolarTimes is null)
                {
                    _logger.LogError("No cached solar times are available. SolarOffset events cannot be scheduled for {ScheduleDate}; DailyTime events will continue to run.", scheduleDate);
                    return null;
                }

                var adjustedSolarTimes = new SolarTimes
                {
                    ScheduleDate = scheduleDate,
                    Sunrise = scheduleDate.Add(cachedSolarTimes.Sunrise.TimeOfDay),
                    Sunset = scheduleDate.Add(cachedSolarTimes.Sunset.TimeOfDay)
                };

                _logger.LogWarning("Using cached solar times from {CachedScheduleDate} for {ScheduleDate}. Sunrise: {Sunrise}, Sunset: {Sunset}", cachedSolarTimes.ScheduleDate, scheduleDate, adjustedSolarTimes.Sunrise, adjustedSolarTimes.Sunset);
                return adjustedSolarTimes;
            }
        }

        private List<ScheduledSwitchEvent> BuildDailySchedule(DateTime scheduleDate, SolarTimes solarTimes)
        {
            var scheduledEvents = new List<ScheduledSwitchEvent>();

            foreach (var device in _deviceManager.Devices.OfType<ISwitchDevice>())
            {
                foreach (var schedule in GetMirandaSchedules(device.Name))
                {
                    foreach (var ev in schedule.Events)
                    {
                        var scheduledAt = ResolveEventTime(scheduleDate, ev.Type, ev.Time, ev.SolarEvent, ev.OffsetMinutes, solarTimes);

                        if (scheduledAt.HasValue)
                        {
                            scheduledEvents.Add(new ScheduledSwitchEvent(device, schedule.SwitchId, ev.Action, scheduledAt.Value));
                        }
                    }
                }

                foreach (var schedule in GetOberonSchedules(device.Name))
                {
                    foreach (var ev in schedule.Events)
                    {
                        var scheduledAt = ResolveEventTime(scheduleDate, ev.Type, ev.Time, ev.SolarEvent, ev.OffsetMinutes, solarTimes);

                        if (scheduledAt.HasValue)
                        {
                            scheduledEvents.Add(new ScheduledSwitchEvent(device, null, ev.Action, scheduledAt.Value));
                        }
                    }
                }
            }

            _logger.LogInformation("Built {EventCount} scheduled events for {ScheduleDate}.", scheduledEvents.Count, scheduleDate);

            return scheduledEvents;
        }

        
        private void SaveSolarTimesToCache(SolarTimes solarTimes)
        {
            try
            {
                var json = JsonConvert.SerializeObject(solarTimes, Formatting.Indented);
                File.WriteAllText(_solarCacheFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save solar times cache to {CacheFilePath}.", _solarCacheFilePath);
            }
        }

        private SolarTimes LoadSolarTimesFromCache()
        {
            try
            {
                if (!File.Exists(_solarCacheFilePath))
                {
                    return null;
                }

                var json = File.ReadAllText(_solarCacheFilePath);
                return JsonConvert.DeserializeObject<SolarTimes>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load solar times cache from {CacheFilePath}.", _solarCacheFilePath);
                return null;
            }
        }

        
        private IEnumerable<MirandaSwitchSchedule> GetMirandaSchedules(string deviceName) =>
            _mirandaSchedule.Switches.Where(s => string.Equals(s.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

        private IEnumerable<OberonDeviceSchedule> GetOberonSchedules(string deviceName) =>
            _oberonSchedule.Devices.Where(d => string.Equals(d.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

        private static DateTime? ResolveEventTime(DateTime today, string type, TimeSpan? time, string solarEvent, int offsetMinutes, SolarTimes solarTimes)
        {
            if (type.Equals("DailyTime", StringComparison.OrdinalIgnoreCase))
            {
                return time.HasValue ? today.Add(time.Value) : null;
            }

            if (type.Equals("SolarOffset", StringComparison.OrdinalIgnoreCase))
            {
                if (solarTimes is null)
                {
                    return null;
                }

                var solarBase = solarEvent.Equals("Sunrise", StringComparison.OrdinalIgnoreCase) ? solarTimes.Sunrise : solarTimes.Sunset;
                return solarBase.AddMinutes(offsetMinutes);
            }

            return null;
        }

        private sealed record ScheduledSwitchEvent(ISwitchDevice Device, string SwitchId, string Action, DateTime ScheduledAt)
        {
            public string TargetKey => $"{Device.Name}|{SwitchId}";
            public string EventKey => $"{TargetKey}|{Action}|{ScheduledAt:O}";
        }
    }
}

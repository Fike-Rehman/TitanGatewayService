using Microsoft.Extensions.Options;
using TitanGatewayService.Devices.Core;
using TitanGatewayService.Devices.Miranda;
using TitanGatewayService.Devices.Oberon;

namespace TitanGatewayService.Scheduling
{
    public sealed class ScheduleExecutionService : BackgroundService
    {
        private readonly ILogger<ScheduleExecutionService> _logger;
        private readonly DeviceManager _deviceManager;
        private readonly SolarApiClient _solarApiClient;
        private readonly MirandaScheduleOptions _mirandaSchedule;
        private readonly OberonScheduleOptions _oberonSchedule;
        private readonly Dictionary<string, DateTime> _lastExecutionByKey = new(StringComparer.OrdinalIgnoreCase);

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
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExecuteDueActionsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while executing scheduled actions.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task ExecuteDueActionsAsync(CancellationToken cancellationToken)
        {
            var (sunrise, sunset) = await _solarApiClient.GetSolarTimesAsync();
            var now = DateTime.Now;
            var today = now.Date;

            foreach (var device in _deviceManager.Devices.OfType<ISwitchDevice>())
            {
                foreach (var schedule in GetMirandaSchedules(device.Name))
                {
                    foreach (var ev in schedule.Events)
                    {
                        await TryExecuteEventAsync(device, ev.Action, schedule.SwitchId, ResolveEventTime(today, ev.Type, ev.Time, ev.SolarEvent, ev.OffsetMinutes, sunrise, sunset), now, cancellationToken);
                    }
                }

                foreach (var schedule in GetOberonSchedules(device.Name))
                {
                    foreach (var ev in schedule.Events)
                    {
                        await TryExecuteEventAsync(device, ev.Action, null, ResolveEventTime(today, ev.Type, ev.Time, ev.SolarEvent, ev.OffsetMinutes, sunrise, sunset), now, cancellationToken);
                    }
                }
            }
        }

        private async Task TryExecuteEventAsync(ISwitchDevice device, string action, string? switchId, DateTime? scheduledAt, DateTime now, CancellationToken cancellationToken)
        {
            if (!scheduledAt.HasValue)
            {
                return;
            }

            var executionWindow = TimeSpan.FromMinutes(2);
            if (now < scheduledAt.Value || now - scheduledAt.Value > executionWindow)
            {
                return;
            }

            var key = $"{device.Name}|{switchId}|{action}|{scheduledAt:O}";
            if (_lastExecutionByKey.TryGetValue(key, out var lastRun) && lastRun.Date == now.Date)
            {
                return;
            }

            var ping = await device.PingAsync();
            if (!string.Equals(ping, "OK", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Skipping scheduled action {Action} for {DeviceName} (SwitchId: {SwitchId}). Device is offline: {PingStatus}", action, device.Name, switchId ?? "N/A", ping);
                return;
            }

            var result = action.Equals("On", StringComparison.OrdinalIgnoreCase)
                ? await device.TurnOnAsync(switchId, cancellationToken)
                : await device.TurnOffAsync(switchId, cancellationToken);

            _lastExecutionByKey[key] = now;
            _logger.LogInformation("Executed scheduled action {Action} for {DeviceName} (SwitchId: {SwitchId}) at {ScheduledAt}. Result: {Result}", action, device.Name, switchId ?? "N/A", scheduledAt, result);
        }

        private IEnumerable<MirandaSwitchSchedule> GetMirandaSchedules(string deviceName) =>
            _mirandaSchedule.Switches.Where(s => string.Equals(s.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

        private IEnumerable<OberonDeviceSchedule> GetOberonSchedules(string deviceName) =>
            _oberonSchedule.Devices.Where(d => string.Equals(d.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

        private static DateTime? ResolveEventTime(DateTime today, string type, TimeSpan? time, string solarEvent, int offsetMinutes, DateTime sunrise, DateTime sunset)
        {
            if (type.Equals("DailyTime", StringComparison.OrdinalIgnoreCase))
            {
                return time.HasValue ? today.Add(time.Value) : null;
            }

            if (type.Equals("SolarOffset", StringComparison.OrdinalIgnoreCase))
            {
                var solarBase = solarEvent.Equals("Sunrise", StringComparison.OrdinalIgnoreCase) ? sunrise : sunset;
                return solarBase.AddMinutes(offsetMinutes);
            }

            return null;
        }
    }
}

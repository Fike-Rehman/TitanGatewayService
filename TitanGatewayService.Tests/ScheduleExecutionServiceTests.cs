using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TitanGatewayService.Devices.Core;
using TitanGatewayService.Extensions;
using TitanGatewayService.Scheduling;
using static TitanGatewayService.Scheduling.ScheduleExecutionService;

namespace TitanGatewayService.Tests;

public sealed class ScheduleExecutionServiceTests
{
    private static readonly DateTime Day = new(2026, 9, 14);
    private static readonly DateTime Deadline = Day.AddHours(18).AddMinutes(27).AddSeconds(32);

    [Fact]
    public async Task EarlyWakeCrossingDeadlineDoesNotDeferM1Until2315()
    {
        var clock = new ScriptedClock(Deadline.AddSeconds(-1)) { WakeEarlyOnce = true };
        var device = new RecordingDevice(clock);
        await Run(clock,
            new(device, "M1", "On", Deadline),
            new(device, "M2", "Off", Day.AddHours(23).AddMinutes(15)));

        var m1 = Assert.Single(device.Commands, c => c.SwitchId == "M1");
        Assert.Equal(Deadline.AddMilliseconds(1), m1.At);
    }

    [Fact]
    public async Task CommandCrossingNextDeadlineDoesNotDeferNextEvent()
    {
        var clock = new ScriptedClock(Deadline.AddSeconds(-1));
        var device = new RecordingDevice(clock)
        {
            AfterCommand = id => { if (id == "M2") clock.Now = clock.Now.AddSeconds(2); }
        };
        await Run(clock,
            new(device, "M2", "On", Deadline),
            new(device, "M1", "On", Deadline.AddSeconds(1)),
            new(device, "M3", "Off", Day.AddHours(23).AddMinutes(15)));

        Assert.Equal(Deadline.AddSeconds(2), Assert.Single(device.Commands, c => c.SwitchId == "M1").At);
    }

    [Fact]
    public async Task AlreadyOverdueEventExecutesWithoutWaiting()
    {
        var clock = new ScriptedClock(Deadline.AddMinutes(1));
        var device = new RecordingDevice(clock);
        await Run(clock, new ScheduledSwitchEvent(device, "M1", "On", Deadline));

        Assert.Equal(Deadline.AddMinutes(1), Assert.Single(device.Commands).At);
        Assert.Equal(Day.AddDays(1).AddMinutes(5), Assert.Single(clock.WakeTargets));
    }

    [Fact]
    public async Task SimultaneousAndFutureEventsExecuteOnceAndRefreshAt0005()
    {
        var clock = new ScriptedClock(Deadline.AddMinutes(-1));
        var device = new RecordingDevice(clock);
        await Run(clock,
            new(device, "M2", "On", Deadline),
            new(device, "M3", "On", Deadline),
            new(device, "M1", "On", Deadline.AddMinutes(30)));

        Assert.Equal(new[] { "M2", "M3", "M1" }, device.Commands.Select(c => c.SwitchId));
        Assert.Equal(new[] { Deadline, Deadline, Deadline.AddMinutes(30) }, device.Commands.Select(c => c.At));
        Assert.Equal(new[] { Deadline, Deadline.AddMinutes(30), Day.AddDays(1).AddMinutes(5) }, clock.WakeTargets);
    }

    [Fact]
    public async Task CancellationDuringWaitDoesNotExecuteEvent()
    {
        using var cancellation = new CancellationTokenSource();
        var clock = new ScriptedClock(Deadline.AddSeconds(-1)) { BeforeWake = cancellation.Cancel };
        var device = new RecordingDevice(clock);
        using var service = CreateService();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteRemainingScheduleForTodayAsync(
            [new(device, "M1", "On", Deadline)], Day, cancellation.Token, clock));
        Assert.Empty(device.Commands);
    }

    private static async Task Run(ScriptedClock clock, params ScheduledSwitchEvent[] events)
    {
        using var service = CreateService();
        await service.ExecuteRemainingScheduleForTodayAsync(events.ToList(), Day, CancellationToken.None, clock);
    }

    private static ScheduleExecutionService CreateService() => new(
        NullLogger<ScheduleExecutionService>.Instance,
        new DeviceManager(new EmptyConfiguration(), new UnusedFactory()),
        new SolarApiClient(new HttpClient(), Options.Create(new SolarApiClientOptions { BaseUrl = "https://example.invalid/" })),
        null, null);

    private sealed class EmptyConfiguration : IDeviceConfigurationManager
    {
        public IReadOnlyList<DeviceConfig> GetAllDevices() => [];
    }

    private sealed class UnusedFactory : IDeviceFactory
    {
        public IDeviceClient Create(DeviceConfig config) => throw new InvalidOperationException("No real devices in tests.");
    }

    private sealed class RecordingDevice(ScriptedClock clock) : ISwitchDevice
    {
        public string Name => "Test Miranda";
        public string Location => "Test";
        public string BaseUrl => "https://example.invalid/";
        public List<(string SwitchId, DateTime At)> Commands { get; } = [];
        public Action<string>? AfterCommand { get; init; }
        public Task<string> PingAsync() => Task.FromResult("OK");
        public Task<string> TurnOnAsync(string switchId = null!, CancellationToken cancellationToken = default) => Record(switchId);
        public Task<string> TurnOffAsync(string switchId = null!, CancellationToken cancellationToken = default) => Record(switchId);
        private Task<string> Record(string id)
        {
            Commands.Add((id, clock.Now));
            AfterCommand?.Invoke(id);
            return Task.FromResult("OK");
        }
    }

    // Complete waits synchronously with a controlled wall clock. No real timers or network calls.
    private sealed class ScriptedClock(DateTime initial) : TimeProvider
    {
        public DateTime Now { get; set; } = initial;
        public bool WakeEarlyOnce { get; set; }
        public Action? BeforeWake { get; init; }
        public List<DateTime> WakeTargets { get; } = [];
        private bool crossDeadlineAfterRead;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow()
        {
            var result = new DateTimeOffset(Now, TimeSpan.Zero);
            if (crossDeadlineAfterRead)
            {
                Now = Now.AddMilliseconds(2);
                crossDeadlineAfterRead = false;
            }
            return result;
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.True(WakeTargets.Count < 20, "Scheduler must not spin indefinitely.");
            Now += dueTime;
            WakeTargets.Add(Now);
            if (WakeEarlyOnce)
            {
                Now = Now.AddMilliseconds(-1);
                WakeEarlyOnce = false;
                crossDeadlineAfterRead = true;
            }
            if (BeforeWake is null) callback(state);
            else BeforeWake();
            return new CompletedTimer();
        }
        private sealed class CompletedTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => false;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}


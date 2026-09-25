using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TitanGatewayService.Devices.Core;
using TitanGatewayService.Devices.Miranda;
using TitanGatewayService.Extensions;
using TitanGatewayService.Scheduling;
using static TitanGatewayService.Scheduling.ScheduleExecutionService;

namespace TitanGatewayService.Tests;

public sealed class ScheduleExecutionServiceTests
{
    private static readonly DateTime Day = new(2026, 9, 14);

    [Fact]
    public async Task PollsEvery60SecondsAndExecutesSolarEventOnNextPollOnlyOnce()
    {
        using var h = new Harness(Day.AddHours(18).AddMinutes(27), 3);
        await h.Run();
        Assert.Equal(new[] { "Off", "On" }, h.Device.Commands.Select(c => c.Action));
        Assert.Equal(Day.AddHours(18).AddMinutes(28), h.Device.Commands[1].At);
        Assert.All(h.Clock.Delays, d => Assert.Equal(TimeSpan.FromSeconds(60), d));
        Assert.Equal(new[] { Day.AddDays(-1), Day }, h.Solar.Dates);
    }

    [Theory]
    [InlineData(3, "Off")]
    [InlineData(19, "On")]
    [InlineData(23, "Off")]
    public async Task RestartRefreshesSolarAndImmediatelyReassertsCurrentState(int hour, string expected)
    {
        using var h = new Harness(Day.AddHours(hour), 1);
        await h.Run();
        await h.Run(); // Same service instance: even its in-memory handled events must reset.
        Assert.Equal(new[] { expected, expected }, h.Device.Commands.Select(c => c.Action));
        Assert.All(h.Device.Commands, c => Assert.Equal(Day.AddHours(hour), c.At));
        Assert.Equal(new[] { Day.AddDays(-1), Day, Day.AddDays(-1), Day }, h.Solar.Dates);
    }

    [Fact]
    public async Task RestartBeforeFirstEventPreservesOvernightOnState()
    {
        using var h = new Harness(Day.AddHours(3), 1, offHour: 7);
        await h.Run();
        Assert.Equal("On", Assert.Single(h.Device.Commands).Action);
    }

    [Fact]
    public async Task DelayedPollAppliesLatestStateWithoutReplayingObsoleteTransitions()
    {
        using var h = new Harness(Day, 1);
        List<ScheduledSwitchEvent> events = [
            new(h.Device, "M1", "On", Day.AddHours(7)),
            new(h.Device, "M1", "Off", Day.AddHours(14)),
            new(h.Device, "M1", "On", Day.AddHours(18)),
            new(h.Device, "M2", "On", Day.AddHours(18))];
        await h.Service.ApplyDueEventsAsync(events, Day.AddHours(17), CancellationToken.None);
        await h.Service.ApplyDueEventsAsync(events, Day.AddHours(19), CancellationToken.None);
        await h.Service.ApplyDueEventsAsync(events, Day.AddHours(20), CancellationToken.None);
        Assert.Equal(new[] { ("M1", "Off"), ("M1", "On"), ("M2", "On") },
            h.Device.Commands.Select(c => (c.Id, c.Action)));
    }

    [Fact]
    public async Task StartupSetsEachSwitchToItsOwnLatestState()
    {
        using var h = new Harness(Day.AddHours(3), 1);
        List<ScheduledSwitchEvent> events = [
            new(h.Device, "M1", "On", Day.AddDays(-1).AddHours(18)),
            new(h.Device, "M1", "Off", Day.AddDays(-1).AddHours(23)),
            new(h.Device, "M2", "On", Day.AddDays(-1).AddHours(18)),
            new(h.Device, "M3", "Off", Day.AddDays(-1).AddHours(23)),
            new(h.Device, "M3", "On", Day.AddHours(2)),
            new(h.Device, "M1", "On", Day.AddHours(18))];
        await h.Service.ApplyDueEventsAsync(events, h.Clock.Now, CancellationToken.None);
        Assert.Equal(3, h.Device.Commands.Count);
        Assert.Equal("Off", Assert.Single(h.Device.Commands, c => c.Id == "M1").Action);
        Assert.Equal("On", Assert.Single(h.Device.Commands, c => c.Id == "M2").Action);
        Assert.Equal("On", Assert.Single(h.Device.Commands, c => c.Id == "M3").Action);
    }

    [Fact]
    public async Task FirstPollAfterMidnightRebuildsScheduleIncludingEventsBefore0005()
    {
        using var h = new Harness(Day.AddHours(23).AddMinutes(59).AddSeconds(30), 3, offHour: 0);
        await h.Run();
        Assert.Equal(new[] { "On", "Off" }, h.Device.Commands.Select(c => c.Action));
        Assert.Equal(Day.AddDays(1).AddSeconds(30), h.Device.Commands[1].At);
        Assert.Equal(new[] { Day.AddDays(-1), Day, Day, Day.AddDays(1) }, h.Solar.Dates);
    }

    [Fact]
    public async Task CancellationDuringPollingWaitExitsWithoutExecutingFutureEvent()
    {
        using var h = new Harness(Day.AddHours(18), 1);
        await h.Run();
        Assert.Equal("Off", Assert.Single(h.Device.Commands).Action);
        Assert.Single(h.Clock.Delays);
    }

    private sealed class Harness : IDisposable
    {
        public PollClock Clock { get; }
        public RecordingDevice Device { get; }
        public SolarHandler Solar { get; } = new();
        public ScheduleExecutionService Service { get; }
        private readonly HttpClient client;
        private readonly int polls;
        public Harness(DateTime now, int polls, int offHour = 23)
        {
            this.polls = polls;
            Clock = new PollClock(now);
            Device = new RecordingDevice(Clock);
            client = new HttpClient(Solar);
            var options = new MirandaScheduleOptions { Switches = [new MirandaSwitchSchedule
            {
                DeviceName = Device.Name, SwitchId = "M1", Events = [
                    new() { Action = "On", Type = "SolarOffset", SolarEvent = "Sunset", OffsetMinutes = -60 },
                    new() { Action = "Off", Type = "DailyTime", Time = TimeSpan.FromHours(offHour) }]
            }] };
            Service = new ScheduleExecutionService(NullLogger<ScheduleExecutionService>.Instance,
                new DeviceManager(new Configuration(), new Factory(Device)),
                new SolarApiClient(client, Options.Create(new SolarApiClientOptions { BaseUrl = "https://example.invalid/" })),
                Options.Create(options), null);
        }
        public async Task Run()
        {
            using var cancellation = new CancellationTokenSource();
            Clock.RemainingPolls = polls;
            Clock.Cancel = cancellation.Cancel;
            await Service.RunSchedulerAsync(cancellation.Token, Clock);
        }
        public void Dispose() { Service.Dispose(); client.Dispose(); }
    }

    private sealed class Configuration : IDeviceConfigurationManager
    {
        public IReadOnlyList<DeviceConfig> GetAllDevices() => [new()];
    }
    private sealed class Factory(RecordingDevice device) : IDeviceFactory
    {
        public IDeviceClient Create(DeviceConfig config) => device;
    }
    private sealed class RecordingDevice(PollClock clock) : ISwitchDevice
    {
        public string Name => "Test Miranda";
        public string Location => "Test";
        public string BaseUrl => "https://example.invalid/";
        public List<(string Id, string Action, DateTime At)> Commands { get; } = [];
        public Task<string> PingAsync() => Task.FromResult("OK");
        public Task<string> TurnOnAsync(string switchId = null!, CancellationToken cancellationToken = default) => Record(switchId, "On");
        public Task<string> TurnOffAsync(string switchId = null!, CancellationToken cancellationToken = default) => Record(switchId, "Off");
        private Task<string> Record(string id, string action)
        {
            Commands.Add((id, action, clock.Now));
            return Task.FromResult("OK");
        }
    }
    private sealed class SolarHandler : HttpMessageHandler
    {
        public List<DateTime> Dates { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var dateText = request.RequestUri!.Query.Split('&').Single(p => p.StartsWith("date="))[5..];
            var date = DateTime.ParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            Dates.Add(date);
            // Return UTC API timestamps that resolve to fixed local solar times on any test machine.
            var sunrise = DateTime.SpecifyKind(date.AddHours(7), DateTimeKind.Local).ToUniversalTime();
            var sunset = DateTime.SpecifyKind(date.AddHours(19).AddMinutes(27).AddSeconds(32), DateTimeKind.Local).ToUniversalTime();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { status = "OK", results = new { sunrise, sunset } }))
            });
        }
    }
    // Synchronous virtual waits keep tests deterministic, with no real timers or network traffic.
    private sealed class PollClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; private set; } = now;
        public int RemainingPolls { get; set; }
        public Action Cancel { get; set; } = null!;
        public List<TimeSpan> Delays { get; } = [];
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            if (--RemainingPolls <= 0) Cancel();
            else { Now += dueTime; callback(state); }
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

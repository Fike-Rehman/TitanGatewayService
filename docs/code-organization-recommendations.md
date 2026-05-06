# TitanGatewayService Code Organization Recommendations

## Observed friction points

1. **Composition root is crowded**: `Program.cs` currently mixes host setup, Serilog wiring, options binding, and service registrations in one file.
2. **Factory growth risk**: `DeviceFactory` currently uses a type switch to instantiate concrete clients; this becomes brittle as more device types are added.
3. **Configuration shape is spread across strings**: sections like `SolarServiceApi`, `MirandaSchedule`, `OberonSchedule`, and `Devices` are referenced by string names in multiple places.
4. **Worker has too many responsibilities**: startup reporting, schedule formatting, and health polling are all in `Worker`.
5. **HttpClient registration anti-pattern**: `BuildServiceProvider()` is used inside service registration, which can create duplicate containers and subtle DI issues.

## Suggested structure

```text
TitanGatewayService/
  Composition/
    ServiceRegistration/
      LoggingRegistration.cs
      OptionsRegistration.cs
      DeviceRegistration.cs
      HttpClientRegistration.cs
  Devices/
    Core/
      IDeviceClient.cs
      IDeviceFactory.cs
      DeviceFactory.cs
      DeviceConfig.cs
      DeviceConfiguration/
        DeviceConfigurationManager.cs
        DeviceCatalogOptions.cs
    Miranda/
      MirandaClient.cs
      MirandaScheduleOptions.cs
      MirandaScheduleReporter.cs
    Oberon/
      OberonClient.cs
      OberonScheduleOptions.cs
      OberonScheduleReporter.cs
  Scheduling/
    IScheduleReporter.cs
    SchedulePrinter.cs
  Monitoring/
    DeviceHealthMonitor.cs
  Hosting/
    Worker.cs
  Program.cs
```

## Concrete refactor plan (incremental)

### Phase 1: Registration cleanup (safe / low risk)

- Create extension groups:
  - `AddTitanLogging(...)`
  - `AddTitanOptions(...)`
  - `AddTitanDevices(...)`
  - `AddTitanHttpClients(...)`
- Keep `Program.cs` as a thin orchestration file that calls these methods in order.
- Remove `BuildServiceProvider()` usage from registrations and rely on overloads that provide `IServiceProvider` in `AddHttpClient` configuration.

### Phase 2: Strongly typed configuration consolidation

- Introduce top-level options classes:
  - `DeviceCatalogOptions` for `Devices`
  - `SolarApiClientOptions` remains but ensure key names are consistent with json fields.
- Introduce constants (or static `ConfigurationKeys`) for section names to remove repeated string literals.
- Validate options with `ValidateOnStart` where practical.

### Phase 3: Factory/instantiation hardening

- Replace switch-based `DeviceFactory` with one of:
  1. **Dictionary of activators** keyed by device type; or
  2. **Per-device registration strategy** (`IDeviceClientBuilder` per type).
- This allows adding a new device without modifying one central switch.

### Phase 4: Worker decomposition

- Move schedule rendering to a dedicated `SchedulePrinter`.
- Move ping loop to `DeviceHealthMonitor`.
- Keep `Worker` focused on lifecycle orchestration.

## Quick wins you can do now

1. Move all service registration from `Program.cs` into extension classes.
2. Stop using `BuildServiceProvider()` in `HttpClientRegistrationExtensions`.
3. Extract schedule event formatting from `Worker` into reusable formatter classes.
4. Introduce `ConfigurationKeys` constants for section names.
5. Create `Devices/Core` folder to hold cross-device abstractions.

## Why this will help

- **Scales with more devices** by reducing central switch edits.
- **Lowers cognitive load** by making startup and registration intent obvious.
- **Reduces DI/config bugs** by removing container construction during registration.
- **Improves testability** by separating concerns in `Worker`.

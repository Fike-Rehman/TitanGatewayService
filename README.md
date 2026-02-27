# TitanGatewayService

TitanGatewayService is a .NET Worker Service that runs as a Windows Service and controls configured devices on a schedule.

## Prerequisites

### Development machine
- Windows with PowerShell 5.1+.
- .NET SDK that supports `net10.0`.
- Network connectivity and PowerShell remoting access to the target computer.
- Permission to administer the remote service and write to `C:\Program Files\CTS\TitanGatewayService`.

### Target computer
- Windows machine with WinRM/PowerShell remoting enabled.
- Service install path available at:
  - `C:\Program Files\CTS\TitanGatewayService`
- Log directory configured in `appsettings.json`:
  - `C:\CTS\Logs`

## One-time setup notes

This repository includes an automated deployment script:
- `Deploy/Build-And-Deploy.ps1`

By default, the script will:
1. Publish the service (`dotnet publish`) using `DefaultProfile`.
2. Copy publish output to a timestamped remote release folder.
3. Stop the existing service (if running).
4. Back up `current` to `backups`.
5. Promote the new release into `current`.
6. Create the Windows service if it does not exist.
7. Start the service and verify it reaches `Running` state.
8. Trim old backups/releases.

## Build and deploy (first deployment)

From a PowerShell session **run as Administrator** on the development machine:

```powershell
Set-Location <repo-root>\Deploy
.\Build-And-Deploy.ps1
```

If you need to target a different machine or service name, pass parameters:

```powershell
.\Build-And-Deploy.ps1 `
  -RemoteComputer "TARGET_HOSTNAME" `
  -ServiceName "TitanGatewayService" `
  -DisplayName "Titan Gateway Service" `
  -InstallPath "C:\Program Files\CTS\TitanGatewayService"
```

### What to expect on target machine after deploy

Directory layout:

```text
C:\Program Files\CTS\TitanGatewayService\
  current\
  releases\release_yyyyMMdd_HHmmss\
  backups\backup_yyyyMMdd_HHmmss\
```

The service executable path should resolve to:

```text
C:\Program Files\CTS\TitanGatewayService\current\TitanGatewayService.exe
```

## Deploy again after code updates

After making code changes:
1. Commit and push your updates (recommended).
2. Re-run the same deployment command from `Deploy`:

```powershell
.\Build-And-Deploy.ps1
```

That command handles stop/backup/promote/start automatically.

If you changed environment-specific configuration values:
- Ensure `appsettings.json` exists in the deployed `current` folder.
- Keep the filename exactly `appsettings.json`.
- Restart by rerunning the deployment script (preferred) instead of manually copying partial files.

## Post-deploy validation checklist

Use a remote PowerShell session from your development/admin machine so you do not need to log on interactively to the target host.

```powershell
$session = New-PSSession -ComputerName "TARGET_HOSTNAME"
```

> Replace `TARGET_HOSTNAME` with your deployed service computer name.

## 1) Verify service status

```powershell
Invoke-Command -Session $session -ScriptBlock {
    Get-Service -Name TitanGatewayService
}
```

Expected: `Status` is `Running`.

Optional detailed check:

```powershell
Invoke-Command -Session $session -ScriptBlock {
    sc.exe qc TitanGatewayService
}
```

Confirm `BINARY_PATH_NAME` points to the `current\TitanGatewayService.exe` location.

## 2) Verify logs are being written

Production log path is configured as `C:\CTS\Logs`, and logs are written as rolling files named `TitanGateway-*.log`.

Check newest log file:

```powershell
Invoke-Command -Session $session -ScriptBlock {
    Get-ChildItem "C:\CTS\Logs\TitanGateway-*.log" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 5
}
```

Tail the latest log:

```powershell
Invoke-Command -Session $session -ScriptBlock {
    $latestLog = Get-ChildItem "C:\CTS\Logs\TitanGateway-*.log" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($latestLog) {
        Get-Content $latestLog.FullName -Tail 100
    }
}
```

Expected startup entries include the service boot message and no repeated fatal exceptions.

## 3) If service fails to start

1. Check Windows Event Viewer:
   - `Windows Logs > Application`
2. Confirm config file exists:

```powershell
Invoke-Command -Session $session -ScriptBlock {
    Test-Path "C:\Program Files\CTS\TitanGatewayService\current\appsettings.json"
}
```

3. Confirm service path is correct:

```powershell
Invoke-Command -Session $session -ScriptBlock {
    sc.exe qc TitanGatewayService
}
```

4. Re-run deployment to restore a clean, complete `current` directory:

```powershell
Set-Location <repo-root>\Deploy
.\Build-And-Deploy.ps1
```

Because deployment is release-folder based and includes rollback behavior, rerunning the script is the safest way to recover from partial/manual file changes.

When you are done with validation, close the remote session:

```powershell
Remove-PSSession $session
```

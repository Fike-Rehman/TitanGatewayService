#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ProjectPath = "..\TitanGatewayService\TitanGatewayService.csproj",

    [string]$PublishProfile = "DefaultProfile",
    [string]$Configuration = "Release",
    [string]$LocalPublishDir = "..\TitanGatewayService\bin\Release\net10.0\publish",
    [string]$RemoteComputer = "mushtari",
    [string]$ServiceName = "TitanGatewayService",
    [string]$InstallPath = "C:\Program Files\CTS\TitanGatewayService",
    [string]$DisplayName = "Titan Gateway Service",
    [int]$KeepBackups = 3,
    [int]$KeepReleases = 5
)

$ErrorActionPreference = "Stop"

function Write-Log {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
}

function Invoke-Robocopy {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination,
        [string[]]$Options = @('/MIR', '/R:2', '/W:2', '/NFL', '/NDL', '/NP')
    )

    $command = @($Source, $Destination) + $Options
    & robocopy @command | Out-Host

    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed with exit code $LASTEXITCODE (Source='$Source', Destination='$Destination')."
    }
}

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
$resolvedLocalPublishDir = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $LocalPublishDir))
$remoteRootShare = "\\$RemoteComputer\$($InstallPath.Substring(0,1))$" + $InstallPath.Substring(2)
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$remoteReleaseShare = Join-Path $remoteRootShare "releases\release_$timestamp"

try {
    Write-Log "Publishing project for self-contained deployment..."
    Write-Log "Project: $resolvedProjectPath"
    Write-Log "Profile: $PublishProfile"
    Write-Log "Configuration: $Configuration"
    Write-Log "Local publish directory: $resolvedLocalPublishDir"

    dotnet publish $resolvedProjectPath -c $Configuration -p:PublishProfile=$PublishProfile -p:PublishDir="$resolvedLocalPublishDir\"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path $resolvedLocalPublishDir)) {
        throw "Publish output directory not found: $resolvedLocalPublishDir"
    }

    $publishedExe = Join-Path $resolvedLocalPublishDir "TitanGatewayService.exe"
    if (-not (Test-Path $publishedExe)) {
        throw "Expected service executable not found in publish output: $publishedExe"
    }

    Write-Log "Ensuring remote base directories exist on $RemoteComputer"
    Invoke-Command -ComputerName $RemoteComputer -ScriptBlock {
        param($RemoteInstallPath)
        $directories = @(
            $RemoteInstallPath,
            (Join-Path $RemoteInstallPath 'current'),
            (Join-Path $RemoteInstallPath 'backups'),
            (Join-Path $RemoteInstallPath 'releases')
        )

        foreach ($dir in $directories) {
            if (-not (Test-Path $dir)) {
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
            }
        }
    } -ArgumentList $InstallPath

    Write-Log "Copying published files to remote release directory: $remoteReleaseShare"
    New-Item -ItemType Directory -Path $remoteReleaseShare -Force | Out-Null
    Invoke-Robocopy -Source $resolvedLocalPublishDir -Destination $remoteReleaseShare

    Write-Log "Activating deployment and restarting service on $RemoteComputer"
    Invoke-Command -ComputerName $RemoteComputer -ScriptBlock {
        param(
            $RemoteInstallPath,
            $RemoteServiceName,
            $RemoteDisplayName,
            $RemoteTimestamp,
            $MaxBackups,
            $MaxReleases
        )

        $ErrorActionPreference = "Stop"

        function Write-Log {
            param([string]$Message)
            Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
        }

        function Invoke-Robocopy {
            param(
                [Parameter(Mandatory = $true)][string]$Source,
                [Parameter(Mandatory = $true)][string]$Destination,
                [string[]]$Options = @('/MIR', '/R:2', '/W:2', '/NFL', '/NDL', '/NP')
            )

            & robocopy $Source $Destination $Options | Out-Host
            if ($LASTEXITCODE -ge 8) {
                throw "robocopy failed with exit code $LASTEXITCODE (Source='$Source', Destination='$Destination')."
            }
        }

        $currentPath = Join-Path $RemoteInstallPath 'current'
        $backupsPath = Join-Path $RemoteInstallPath 'backups'
        $releasesPath = Join-Path $RemoteInstallPath 'releases'
        $releasePath = Join-Path $releasesPath "release_$RemoteTimestamp"
        $backupPath = Join-Path $backupsPath "backup_$RemoteTimestamp"

        try {
            $service = Get-Service -Name $RemoteServiceName -ErrorAction SilentlyContinue

            if ($service -and $service.Status -ne 'Stopped') {
                Write-Log "Stopping service: $RemoteServiceName"
                Stop-Service -Name $RemoteServiceName -Force
                $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
            }

            if (Test-Path $currentPath) {
                Write-Log "Backing up current deployment to $backupPath"
                New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
                Invoke-Robocopy -Source $currentPath -Destination $backupPath
            }

            Write-Log "Promoting release from $releasePath to $currentPath"
            New-Item -ItemType Directory -Path $currentPath -Force | Out-Null
            Invoke-Robocopy -Source $releasePath -Destination $currentPath

            $exePath = Join-Path $currentPath 'TitanGatewayService.exe'
            if (-not (Test-Path $exePath)) {
                throw "Service executable not found at expected path: $exePath"
            }

            if (-not $service) {
                Write-Log "Creating Windows service: $RemoteServiceName"
                sc.exe create $RemoteServiceName binPath= "`"$exePath`"" DisplayName= "`"$RemoteDisplayName`"" start= auto | Out-Host
                sc.exe description $RemoteServiceName "Titan Gateway Service deployed via Build-And-Deploy.ps1" | Out-Host
            }

            Write-Log "Starting service: $RemoteServiceName"
            Start-Service -Name $RemoteServiceName
            $service = Get-Service -Name $RemoteServiceName
            $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))

            if ($service.Status -ne 'Running') {
                throw "Service failed to start. Current status: $($service.Status)"
            }
        }
        catch {
            Write-Log "Deployment failed. Attempting rollback from backup..."
            $latestBackup = Get-ChildItem -Path $backupsPath -Directory -ErrorAction SilentlyContinue | Sort-Object CreationTime -Descending | Select-Object -First 1
            if ($latestBackup) {
                New-Item -ItemType Directory -Path $currentPath -Force | Out-Null
                Invoke-Robocopy -Source $latestBackup.FullName -Destination $currentPath
                if (Get-Service -Name $RemoteServiceName -ErrorAction SilentlyContinue) {
                    Start-Service -Name $RemoteServiceName -ErrorAction SilentlyContinue
                }
                Write-Log "Rollback complete using backup: $($latestBackup.Name)"
            }

            throw
        }

        Write-Log "Cleaning old backups (keep $MaxBackups)"
        $oldBackups = Get-ChildItem -Path $backupsPath -Directory | Sort-Object CreationTime -Descending | Select-Object -Skip $MaxBackups
        foreach ($oldBackup in $oldBackups) {
            Remove-Item -Path $oldBackup.FullName -Recurse -Force
        }

        Write-Log "Cleaning old releases (keep $MaxReleases)"
        $oldReleases = Get-ChildItem -Path $releasesPath -Directory | Sort-Object CreationTime -Descending | Select-Object -Skip $MaxReleases
        foreach ($oldRelease in $oldReleases) {
            Remove-Item -Path $oldRelease.FullName -Recurse -Force
        }

        Write-Log "Deployment succeeded and service is running."
    } -ArgumentList $InstallPath, $ServiceName, $DisplayName, $timestamp, $KeepBackups, $KeepReleases

    Write-Log "Done. Deployment completed successfully."
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    exit 1
}

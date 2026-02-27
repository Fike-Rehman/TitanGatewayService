#Requires -RunAsAdministrator

param(
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath = "..\TitanGatewayService\TitanGatewayService.csproj",
    
    [string]$PublishProfile = "DefaultProfile",
    [string]$Configuration = "Release",
    [string]$PublishPath = "\\Mushtari\cts2\TitanGatewayService",
    [string]$RemoteComputer = "Mushtari",
    [string]$ServiceName = "TitanGatewayService",
    [string]$InstallPath = "C:\Program Files\CTS\TitanGatewayService",
    [string]$DisplayName = "Titan Gateway Service"
)

$ErrorActionPreference = "Stop"

function Write-Log {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
}

try {
    # Step 1: Build and publish on local machine
    Write-Log "Building and publishing project..."
    Write-Log "Project: $ProjectPath"
    Write-Log "Profile: $PublishProfile"
    Write-Log "Output: $PublishPath"
    
    dotnet publish $ProjectPath -c $Configuration -p:PublishProfile=$PublishProfile
    
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed with exit code $LASTEXITCODE"
    }
    
    Write-Log "Build and publish completed successfully"
    Write-Log ""
    
    # Step 2: Deploy remotely on target machine
    Write-Log "Connecting to remote machine: $RemoteComputer"
    
    $deploymentScript = @"
        `$ErrorActionPreference = "Stop"
        `$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        
        function Write-Log {
            param([string]`$Message)
            Write-Host "[`$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] `$Message"
        }
        
        try {
            # Create install directory if it doesn't exist
            if (-not (Test-Path '$InstallPath')) {
                New-Item -ItemType Directory -Path '$InstallPath' -Force | Out-Null
                New-Item -ItemType Directory -Path '$InstallPath\current' -Force | Out-Null
                New-Item -ItemType Directory -Path '$InstallPath\backups' -Force | Out-Null
                New-Item -ItemType Directory -Path '$InstallPath\config' -Force | Out-Null
                Write-Log "Created install directory structure at: $InstallPath"
            }
        
            # Check if service exists
            `$service = Get-Service -Name '$ServiceName' -ErrorAction SilentlyContinue
        
            # Stop the service if running
            if (`$service) {
                Write-Log "Stopping service: $ServiceName"
                Stop-Service -Name '$ServiceName' -Force
                Start-Sleep -Seconds 2
            }
        
            # Backup current version
            `$currentPath = '$InstallPath\current'
            if (Test-Path `$currentPath) {
                `$backupPath = '$InstallPath\backups\backup_`$timestamp'
                Write-Log "Backing up current version to: `$backupPath"
                Copy-Item -Path `$currentPath -Destination `$backupPath -Recurse -Force
                
                # Clean up old backups (keep last 3 versions)
                `$backups = Get-ChildItem -Path '$InstallPath\backups' -Directory | Sort-Object CreationTime -Descending
                if (`$backups.Count -gt 3) {
                    `$backups | Select-Object -Skip 3 | ForEach-Object {
                        Write-Log "Removing old backup: `$(`$_.Name)"
                        Remove-Item -Path `$_.FullName -Recurse -Force
                    }
                }
            }
        
            # Deploy new version
            Write-Log "Deploying new version from: $PublishPath"
            Remove-Item -Path `$currentPath -Recurse -Force -ErrorAction SilentlyContinue
            Copy-Item -Path '$PublishPath' -Destination `$currentPath -Recurse -Force
        
            # Copy shared config if it exists
            `$configFile = '$InstallPath\config\appsettings.json'
            if (Test-Path `$configFile) {
                Write-Log "Restoring appsettings.json"
                Copy-Item -Path `$configFile -Destination "`$currentPath\appsettings.json" -Force
            }
        
            # Create or update Windows Service
            if (-not `$service) {
                Write-Log "Creating Windows Service: $ServiceName"
                sc.exe create '$ServiceName' binPath= "`$currentPath\TitanGatewayService.exe" DisplayName= "$DisplayName" start= auto
                sc.exe config '$ServiceName' start= auto
            }
        
            # Start the service
            Write-Log "Starting service: $ServiceName"
            Start-Service -Name '$ServiceName'
            Start-Sleep -Seconds 3
        
            # Verify service is running
            `$service = Get-Service -Name '$ServiceName'
            if (`$service.Status -eq "Running") {
                Write-Log "SUCCESS: Service is running"
            } else {
                throw "Service failed to start. Status: `$(`$service.Status)"
            }
        
            Write-Log "Deployment completed successfully"
        }
        catch {
            Write-Log "ERROR: `$(`$_.Exception.Message)"
            
            # Attempt rollback
            Write-Log "Attempting rollback..."
            `$latestBackup = Get-ChildItem -Path '$InstallPath\backups' -Directory | Sort-Object CreationTime -Descending | Select-Object -First 1
            
            if (`$latestBackup) {
                Remove-Item -Path `$currentPath -Recurse -Force
                Copy-Item -Path `$latestBackup.FullName -Destination `$currentPath -Recurse -Force
                Write-Log "Rolled back to: `$(`$latestBackup.Name)"
                Start-Service -Name '$ServiceName'
            }
            
            throw
        }
"@

    Write-Log "Deploying to remote machine: $RemoteComputer"
    Invoke-Command -ComputerName $RemoteComputer -ScriptBlock ([scriptblock]::Create($deploymentScript))
    
    Write-Log "Remote deployment completed successfully!"
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    exit 1
}
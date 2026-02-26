#Requires -RunAsAdministrator

param(
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath = ".\TitanGatewayService\TitanGatewayService.csproj",
    
    [string]$PublishProfile = "DefaultProfile",
    [string]$Configuration = "Release",
    [string]$PublishPath = "\\Mushtari\cts2\TitanGatewayService",
    [string]$ServiceName = "TitanGatewayService",
    [string]$InstallPath = "C:\Services\TitanGatewayService",
    [string]$DisplayName = "Titan Gateway Service"
)

$ErrorActionPreference = "Stop"

function Write-Log {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
}

try {
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
    Write-Log "Deploying to target machine..."
    Write-Log "Service Name: $ServiceName"
    Write-Log "Install Path: $InstallPath"
    
    & "$PSScriptRoot\Deploy-TitanGatewayService.ps1" `
        -PublishPath $PublishPath `
        -ServiceName $ServiceName `
        -InstallPath $InstallPath `
        -DisplayName $DisplayName
    
    Write-Log "Deployment completed successfully!"
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    exit 1
}
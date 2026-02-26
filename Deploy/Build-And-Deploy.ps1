param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectPath,
    
    [string]$PublishProfile = "DefaultProfile",  # Your profile name
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "Building and publishing project..." -ForegroundColor Green
dotnet publish $ProjectPath -c $Configuration -p:PublishProfile=$PublishProfile

Write-Host "Deploying..." -ForegroundColor Green
& ".\Deploy-TitanGatewayService.ps1" -PublishPath ".\publish"
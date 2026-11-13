# Windows PowerShell launcher.
# Usage: .\run.ps1 [-Start D] [-English] [-Port 5000]
param(
    [string]$Start = "D",
    [switch]$English,
    [int]$Port = 5000
)
$ErrorActionPreference = "Stop"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".NET 8 SDK not found. Install it from https://dotnet.microsoft.com/download"
}
Set-Location (Join-Path $PSScriptRoot "..\DeliveryRoutes")
$path = if ($English) { "en/" } else { "" }
Start-Process "http://localhost:$Port/$path"
dotnet run -- city.txt packages.txt $Start --web --port $Port --no-open

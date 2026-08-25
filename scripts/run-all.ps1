#Requires -Version 5.1
<#
.SYNOPSIS
  Starts PaymentSim (:5190), the API (:5180), and the Angular app (:4200).

.NOTES
  From a clone:  pwsh -File scripts/run-all.ps1
  Windows:       powershell -File scripts/run-all.ps1
#>
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$web = Join-Path $root 'src\loyaltylab-web'

function Test-Command {
    param([string]$Name)
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Test-Listening {
    param([int]$Port)
    try {
        $client = [System.Net.Sockets.TcpClient]::new()
        $client.Connect('127.0.0.1', $Port)
        $client.Dispose()
        return $true
    }
    catch {
        return $false
    }
}

function Wait-HttpOk {
    param(
        [string]$Url,
        [string]$Name,
        [int]$Seconds = 90
    )

    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                Write-Host "Ready: $Name  $Url"
                return
            }
        }
        catch {
            # still starting
        }

        Start-Sleep -Seconds 1
    }

    throw "Timed out waiting for $Name at $Url."
}

function Start-InWindow {
    param(
        [string]$Title,
        [string]$Command
    )

    $script = @"
Set-Location -LiteralPath '$root'
`$Host.UI.RawUI.WindowTitle = '$Title'
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
Write-Host '$Title'
$Command
"@
    Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoExit', '-NoLogo', '-Command', $script) | Out-Null
}

if (-not (Test-Command 'dotnet')) {
    throw '.NET SDK not found. Install the .NET 10 SDK from https://dotnet.microsoft.com/download and re-open this terminal.'
}

if (-not (Test-Command 'node') -or -not (Test-Command 'npm')) {
    throw 'Node.js / npm not found. Install Node 22 LTS from https://nodejs.org and re-open this terminal.'
}

Write-Host "Repo: $root"
Write-Host 'Installing Angular packages if needed…'
Push-Location $web
try {
    if (-not (Test-Path -LiteralPath (Join-Path $web 'node_modules'))) {
        npm install
        if ($LASTEXITCODE -ne 0) {
            throw 'npm install failed.'
        }
    }
}
finally {
    Pop-Location
}

if (Test-Listening 5190) {
    Write-Host 'PaymentSim already listening on :5190'
}
else {
    Start-InWindow 'LoyaltyLab.PaymentSim :5190' 'dotnet run --project src/LoyaltyLab.PaymentSim --launch-profile http'
}

if (Test-Listening 5180) {
    Write-Host 'API already listening on :5180'
}
else {
    Start-InWindow 'LoyaltyLab.Api :5180' 'dotnet run --project src/LoyaltyLab.Api --launch-profile http'
}

if (Test-Listening 4200) {
    Write-Host 'Angular already listening on :4200'
}
else {
    $webCommand = @"
Set-Location -LiteralPath '$web'
npx ng serve --host 127.0.0.1 --port 4200
"@
    Start-InWindow 'loyaltylab-web :4200' $webCommand
}

Wait-HttpOk 'http://localhost:5190/health' 'PaymentSim'
Wait-HttpOk 'http://localhost:5180/health' 'API'
Wait-HttpOk 'http://127.0.0.1:4200/' 'Angular'

Write-Host ''
Write-Host 'Loyalty Lab is running'
Write-Host '  Web         http://127.0.0.1:4200/'
Write-Host '  API         http://localhost:5180/health'
Write-Host '  PaymentSim  http://localhost:5190/health'
Write-Host ''
Write-Host 'Demo identity defaults to Maya · Summit Gold. Close the three process windows to stop.'

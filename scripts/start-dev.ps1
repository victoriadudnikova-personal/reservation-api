#requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Help,
    [switch]$SetupOnly,
    [ValidateRange(30, 600)][int]$DatabaseTimeoutSeconds = 180
)

if ($Help) {
    Write-Host @'
Usage: .\scripts\start-dev.ps1 [-Help] [-SetupOnly] [-DatabaseTimeoutSeconds <seconds>]

  -Help                    Display options and exit without changing anything.
  -SetupOnly               Initialize the database without starting the API.
  -DatabaseTimeoutSeconds  Database readiness timeout, 30-600 seconds (default: 180).

Examples:
  .\scripts\start-dev.ps1
  .\scripts\start-dev.ps1 -SetupOnly
  .\scripts\start-dev.ps1 -DatabaseTimeoutSeconds 300

Missing .NET 9 SDK or Docker: prompts Y/N to install using WinGet on Windows.
Installers may request administrator permission. Docker may require WSL setup
or a restart; start Docker Desktop with Linux containers before continuing.
API: https://localhost:7000/swagger (uses a trusted development certificate).
Ctrl+C stops the API; the database remains running.
'@
    return
}

#examples:
#.\scripts\start-dev.ps1
#.\scripts\start-dev.ps1 -SetupOnly
#.\scripts\start-dev.ps1 -DatabaseTimeoutSeconds 300

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$apiProject = 'ReservationWebAPI/ReservationWebAPI/ReservationWebAPI.csproj'
$environmentNames = @('MSSQL_SA_PASSWORD', 'ConnectionStrings__ReservationDb',
    'ConnectionStrings__ReservationTestDb', 'ASPNETCORE_ENVIRONMENT')
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

function Invoke-Checked {
    param([string]$Command, [string[]]$Arguments)
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command failed (exit code $LASTEXITCODE). See the output above."
    }
}

function Install-Prerequisite {
    param([string]$DisplayName, [string]$PackageId, [string]$MissingMessage)
    $answer = Read-Host "$DisplayName is missing. Download and install it using WinGet? [Y/N]"
    while ($answer.Trim() -notmatch '^(?i:y|yes|n|no)$') {
        $answer = Read-Host 'Please answer Y or N'
    }
    if ($answer.Trim() -match '^(?i:n|no)$') { throw $MissingMessage }
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
        throw "Automatic installation supports Windows only. $MissingMessage"
    }
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw "WinGet is unavailable. Install Microsoft's App Installer, reopen PowerShell, and retry. $MissingMessage"
    }
    Write-Host "Installing $DisplayName. Follow any installer or administrator prompts."
    Invoke-Checked winget @('install', '--id', $PackageId, '--exact', '--source', 'winget')

    # Keep the current PATH and append newly installed machine/user entries.
    foreach ($target in @('Machine', 'User')) {
        $installedPath = [Environment]::GetEnvironmentVariable('Path', $target)
        if ($installedPath) { $env:Path += [IO.Path]::PathSeparator + $installedPath }
    }
    if ($PackageId -eq 'Docker.DockerDesktop') {
        Write-Host 'Start Docker Desktop and complete its setup with Linux containers enabled.'
        Write-Host 'If Windows needs a restart, restart it and rerun this script.'
        Read-Host 'Press Enter once Docker Desktop is ready' | Out-Null
    }
}

Push-Location $repositoryRoot
try {
    $sdkMissingMessage = 'Install the .NET 9 SDK before running this script.'
    $hasSdk = $false
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        $sdks = & dotnet --list-sdks
        $hasSdk = $LASTEXITCODE -eq 0 -and [bool]($sdks -match '^9\.')
    }
    if (-not $hasSdk) {
        Install-Prerequisite '.NET 9 SDK' 'Microsoft.DotNet.SDK.9' $sdkMissingMessage
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw $sdkMissingMessage }
    $sdks = & dotnet --list-sdks
    if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^9\.')) {
        throw $sdkMissingMessage
    }
    $dockerMissingMessage = 'Install docker and make it available on PATH before running this script.'
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Install-Prerequisite 'Docker Desktop' 'Docker.DockerDesktop' $dockerMissingMessage
    }
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw $dockerMissingMessage }
    Invoke-Checked docker @('compose', 'version')
    $dockerOs = & docker info --format '{{.OSType}}'
    if ($LASTEXITCODE -ne 0 -or $dockerOs -ne 'linux') {
        throw 'Start Docker Desktop with Linux containers enabled, then retry.'
    }

    if (-not $SetupOnly) {
        & dotnet dev-certs https --check --trust
        if ($LASTEXITCODE -ne 0) {
            Write-Host 'Creating and trusting the HTTPS development certificate. Approve the Windows certificate prompt if shown.'
            Invoke-Checked dotnet @('dev-certs', 'https', '--trust')
            Invoke-Checked dotnet @('dev-certs', 'https', '--check', '--trust')
        }
    }

    $credentialPath = Join-Path $repositoryRoot '.local/database-password.txt'
    if (-not (Test-Path -LiteralPath $credentialPath)) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $credentialPath) -Force | Out-Null
        $bytes = New-Object byte[] 32
        $random = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $random.GetBytes($bytes) } finally { $random.Dispose() }
        $password = 'Dev1!' + [Convert]::ToBase64String($bytes)
        [IO.File]::WriteAllText($credentialPath, $password)
        Write-Host 'Generated local development credentials in the ignored .local directory.'
    }
    $password = [IO.File]::ReadAllText($credentialPath).Trim()
    if ([string]::IsNullOrWhiteSpace($password)) {
        throw 'The local password file is empty. Restore it before starting the existing database.'
    }
    $env:MSSQL_SA_PASSWORD = $password
    $connection = "Server=127.0.0.1,1637;Database=ReservationDb;User Id=sa;Password=$password;Encrypt=True;TrustServerCertificate=True"
    $env:ConnectionStrings__ReservationDb = $connection
    $env:ConnectionStrings__ReservationTestDb = $connection.Replace('Database=ReservationDb;', 'Database=ReservationDb_Tests;')
    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    Write-Host 'Starting the database and waiting for its health check...'
    Invoke-Checked docker @('compose', '-f', 'compose.dev.yaml', 'up', '-d', '--wait',
        '--wait-timeout', "$DatabaseTimeoutSeconds", 'db')

    Write-Host 'Applying database migrations and meeting-room seed data...'
    Invoke-Checked dotnet @('run', '--project', $apiProject, '--no-launch-profile', '--', '--initialize-database')
    if ($SetupOnly) {
        Write-Host 'Setup complete. Database remains running on localhost:1637.'
        return
    }

    Write-Host 'Starting API. Once the server is listening, open https://localhost:7000/swagger'
    Write-Host 'Press Ctrl+C to stop the API. The database remains running.'
    Invoke-Checked dotnet @('run', '--project', $apiProject, '--no-build', '--no-launch-profile',
        '--', '--urls', 'https://localhost:7000')
}
finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
    Pop-Location
}

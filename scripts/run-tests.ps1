#requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Help,
    [ValidateRange(30, 600)][int]$DatabaseTimeoutSeconds = 180
)

if ($Help) {
    Write-Host @'
Usage: .\scripts\run-tests.ps1 [-Help] [-DatabaseTimeoutSeconds <seconds>]

  -Help                    Display options without running setup or tests.
  -DatabaseTimeoutSeconds  Database readiness timeout, 30-600 seconds (default: 180).

Starts the development database through start-dev.ps1 -SetupOnly, then runs
the NUnit suite against ReservationDb_Tests. The API is not started.
The database remains running afterward. Failed tests produce a nonzero exit.

If scripts are disabled, run this in the terminal BEFORE launching the script:
  Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned
'@
    return
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$previousConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__ReservationTestDb', 'Process')
$testExitCode = 0

Push-Location $repositoryRoot
try {
    & (Join-Path $PSScriptRoot 'start-dev.ps1') -SetupOnly -DatabaseTimeoutSeconds $DatabaseTimeoutSeconds
    $password = [IO.File]::ReadAllText((Join-Path $repositoryRoot '.local/database-password.txt')).Trim()
    if ([string]::IsNullOrWhiteSpace($password)) { throw 'The local database password file is empty.' }
    $env:ConnectionStrings__ReservationTestDb = "Server=127.0.0.1,1637;Database=ReservationDb_Tests;User Id=sa;Password=$password;Encrypt=True;TrustServerCertificate=True"
    & dotnet test UnitTests/UnitTests.csproj
    $testExitCode = $LASTEXITCODE
}
finally {
    [Environment]::SetEnvironmentVariable('ConnectionStrings__ReservationTestDb', $previousConnection, 'Process')
    Pop-Location
}

exit $testExitCode

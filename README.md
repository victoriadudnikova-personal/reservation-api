# Reservation API

A small ASP.NET Core API for reserving office meeting rooms, built with .NET 9 and Entity Framework Core. It supports booking and confirming reservations, prevents overlapping bookings, handles repeated requests through idempotency, and cleans up expired reservations and idempotency records in the background.

## Prerequisites

- Windows PowerShell 5.1 or newer for the startup script
- .NET 9 SDK (the script can offer to install it)
- Docker Desktop with Docker Compose v2, Linux containers enabled, and the Docker engine running (the script can offer to install Docker Desktop)
- WinGet (Microsoft App Installer) if you want the script to install missing prerequisites

Run the following commands from the repository root, the directory containing `README.md` and `compose.dev.yaml`. If your terminal is already inside the `scripts` directory, run `cd ..` first.

## Start the project

Open PowerShell in the repository root and allow local scripts for the current terminal session, then start the project:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned
.\scripts\start-dev.ps1
```

Answer `Y` if PowerShell asks to confirm the execution-policy change. `-Scope Process` applies only to this terminal session and resets when the window closes; it normally requires no administrator rights. Repeat this command in a new PowerShell session if scripts are blocked with a "running scripts is disabled" error. An organization-managed execution policy may override this setting.

The script checks for the .NET 9 SDK, Docker Compose v2, and a running Linux Docker engine. If the SDK or Docker is missing, it asks Y/N before installing it through WinGet on Windows. Answering N stops setup. WinGet (Microsoft App Installer) must already be available; installers may request administrator approval. Docker Desktop may require WSL setup or a restart. Complete its setup and enable Linux containers before continuing.

On first use the script generates a local password in `.local/database-password.txt` (ignored by Git). It starts SQL Server, waits for its health check, applies EF Core migrations including the initial meeting-room seed data, and starts the API at https://localhost:7000/swagger. Before starting the API it checks for a trusted HTTPS development certificate, creating and trusting one when needed. Windows may display a certificate confirmation prompt. Credentials are passed through environment variables and are restored in the calling shell when the script exits.

Display options without running setup:

```powershell
.\scripts\start-dev.ps1 -Help
```

Use `-SetupOnly` to initialize the database without starting the API or setting up HTTPS. Use `-DatabaseTimeoutSeconds 300` to increase the readiness timeout (allowed range: 30-600 seconds; default: 180).

The database uses port **1637** and a persistent Compose volume. Keep the generated password file for subsequent runs: changing it does not change the password stored in an existing SQL Server volume. Local credentials are stored as plaintext on your machine; this setup is for development.

Press Ctrl+C to stop the API. To stop the database without deleting its data:

```powershell
docker compose -f compose.dev.yaml stop
```

## Run the tests

In PowerShell at the repository root, run:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned
.\scripts\run-tests.ps1
```

The runner calls `start-dev.ps1 -SetupOnly`, reads the generated password, configures the test connection, and runs `dotnet test UnitTests/UnitTests.csproj`. It restores the previous test connection and working directory afterward, and returns the test command's exit code. The database remains running.

Use `run-tests.ps1 -Help` to display options, or `run-tests.ps1 -DatabaseTimeoutSeconds 300` to increase the database readiness timeout. The execution-policy command must run in the terminal first because PowerShell checks the policy before executing any script code.

The NUnit suite covers reservations, invalid requests, concurrent bookings, seed data, and background cleanup. Tests automatically create and migrate a separate `ReservationDb_Tests` database. The API does not need to be running.

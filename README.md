# Reservation API

A small ASP.NET Core API for reserving office meeting rooms, built with .NET 9 and Entity Framework Core. It supports booking and confirming reservations, prevents overlapping bookings, handles repeated requests through idempotency, and cleans up expired reservations and idempotency records in the background.

## Prerequisites

- .NET 9 SDK
- Docker with Linux containers enabled and the Docker engine running

Run the following commands from the repository root.

## Run the tests

Create the local database container once, then start it:

```powershell
.\scripts\create-db-containers.bat
.\scripts\run-db-containers.bat
```

On Linux or macOS, use the Bash scripts instead:

```bash
bash scripts/create-db-containers.sh
bash scripts/run-db-containers.sh
```

For later runs, only the start script is needed. Wait until the database is ready to accept connections, then run:

```sh
dotnet test UnitTests/UnitTests.csproj
```

The NUnit suite covers reservations, invalid requests, concurrent bookings, seed data, and background cleanup. These tests require the database at `127.0.0.1:1636`; they automatically create and migrate a separate `ReservationDb_Tests` database. The API does not need to be running. If tests fail with connection errors, check that the container is running and the database has finished starting.

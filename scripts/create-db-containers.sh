#!/usr/bin/env bash
set -euo pipefail

# Create the database container, listening on host port 1636.
docker create \
  --env "ACCEPT_EULA=1" \
  --env "MSSQL_SA_PASSWORD=Agh!8Ds?qL7r2e3u" \
  --publish 1636:1433 \
  --volume vd_reservation_api_database_volume:/var/opt/mssql \
  --name vd-reservation-api-database \
  mcr.microsoft.com/azure-sql-edge:latest

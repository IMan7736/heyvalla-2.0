#!/bin/bash
# Creates one database per agent (docs/PLAN.md section 6) and enables PostGIS in each.
# Runs once, on first start of an empty data directory.
set -euo pipefail

databases=(
  dhathuru_atoll dhathuru_dhoni dhathuru_kandu dhathuru_faru dhathuru_thila dhathuru_lookout
)

# dhathuru_main already exists (POSTGRES_DB).
for db in "${databases[@]}"; do
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres -c "CREATE DATABASE ${db} OWNER ${POSTGRES_USER};"
done

for db in dhathuru_main "${databases[@]}"; do
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$db" -c "CREATE EXTENSION IF NOT EXISTS postgis;"
done

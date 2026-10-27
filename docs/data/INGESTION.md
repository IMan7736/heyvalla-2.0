# Ingestion (KANDU)

Run from the repo root with `dotnet run --project apps/api/src/Dhathuru.DataTools --artifacts-path apps/api/.artifacts/kandu -- <command>`. Options: `--datasets`, `--seed`, `--cache`.

| Command | Reads | Writes | Notes |
|---|---|---|---|
| `profile` | rides, MACL, FollowMe file list | stdout | aggregates only |
| `build-taxi-model` | rides CSV, fare matrix, `seed/catalog.json` (zone centroids) | `seed/taxi-model.json` | streams; groups with n<10 dropped; zone keys only; fails if matrix asymmetric |
| `build-sea-speeds` | 31 FollowMe CSVs | `seed/sea-speeds.json` | per-file cache in `apps/api/.artifacts/kandu/cache` makes reruns resume; 0.01 degree cells, at least 10 devices; fails over 5 MB |
| `build-air-seasonality` | MACL CSV | `seed/air-seasonality.json` | years 2022-2025; no carrier or route codes |
| `validate-seed` | seed dir | stdout | same validator the API uses |
| `import-catalog` | seed dir | Postgres `catalog` schema | applies migrations, then upserts in one transaction; second run reports 0 changes. Needs `Database__Password` (and `Database__Name=dhathuru_kandu`) in the environment |

All outputs are deterministic and written atomically (temp file, then move). `catalog.json` is hand-maintained; its timetables, fares and cutoffs are synthetic and flagged.
Host config: `Catalog:Source=database|seed`, `Catalog:SeedDirectory`.

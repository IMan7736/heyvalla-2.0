# Map feature (COMPASS)

## Tiles
`bash infra/tiles/build.sh` (Docker; ~2 min) writes to `infra/tiles/out/` (git-ignored): `maldives.pmtiles` (OpenMapTiles schema, Planetiler), `land.geojson`, `reefs.geojson`. Ocean polygons are derived from OSM coastlines (`make_water.py`) because the global file is ~930 MB. Dev: `node infra/tiles/serve.mjs` serves them plus `infra/tiles/assets/fonts` on :8081 with Range+CORS. Prod: upload the three files and `fonts/` to object storage with Range support; set `NEXT_PUBLIC_TILES_URL` to its base URL.
Attribution required: © OpenStreetMap contributors, © OpenMapTiles (shown in the map).

## Components (`@/features/map`)
- `<TripMap option geometries? focus? time? onTimeChange? autoPlay? showScrubber? ref />`; handle: `focusLeg, fitRoute, play, pause, seek`. Share focus with a list via `useMapFocus()`.
- `<NetworkMap places serviceCounts? connections? selectedId? onSelectPlace />`
- `<MapPicker places value onChange label />`; `<PlaceSearch />` is the keyboard alternative.
- All are lazy (maplibre bundle loads on demand), show a skeleton, and fall back to lists without WebGL. Wrap in next-intl provider (`map` messages namespace, timeZone Indian/Maldives).

## Honesty
Leg geometry from `GET /geo/legs` is used when present; otherwise sea/air shapes are generated and flagged `approximate` (UI note + per-leg badge). Never navigational.

## Dev
`pnpm --filter web map:lab` (:3107, views `?view=trip|network|picker`), `pnpm --filter web map:e2e`, `pnpm --filter web test`.

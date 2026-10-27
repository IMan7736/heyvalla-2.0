#!/usr/bin/env bash
# Builds infra/tiles/out/maldives.pmtiles from the Geofabrik Maldives extract using Planetiler (Docker).
# Output and downloads live in infra/tiles/out (git-ignored). Re-run to refresh; safe to repeat.
set -euo pipefail
cd "$(dirname "$0")"
OUT="$PWD/out"
mkdir -p "$OUT"
PBF="$OUT/maldives.osm.pbf"
if [ ! -s "$PBF" ] || [ "${REFRESH:-0}" = "1" ]; then
  curl -fL --retry 3 -o "$PBF" https://download.geofabrik.de/asia/maldives-latest.osm.pbf
fi
# Maldives-only ocean polygons from the coastline ways (the official global file is ~930 MB).
mkdir -p "$OUT/sources"
if [ ! -s "$OUT/sources/water-polygons-split-3857.zip" ] || [ "${REFRESH:-0}" = "1" ]; then
  docker run --rm -u "$(id -u):$(id -g)" -v "$PWD:/work" ghcr.io/osgeo/gdal:alpine-normal-latest \
    python3 /work/make_water.py /work/out/maldives.osm.pbf /work/out/sources
fi
docker run --rm -u "$(id -u):$(id -g)" -e JAVA_TOOL_OPTIONS="-Xmx3g" -v "$OUT:/data" \
  ghcr.io/onthegomap/planetiler:latest \
  --osm-path=/data/maldives.osm.pbf --output=/data/maldives.pmtiles \
  --download --force --maxzoom=14 \
  --storage=mmap
# Reefs/lagoons: OpenMapTiles has no reef layer, so they ship as a small GeoJSON beside the PMTiles.
docker run --rm -u "$(id -u):$(id -g)" -v "$PWD:/work" ghcr.io/osgeo/gdal:alpine-normal-latest \
  python3 /work/make_reefs.py /work/out/maldives.osm.pbf /work/out/reefs.geojson
ls -lh "$OUT/maldives.pmtiles" "$OUT/reefs.geojson"

"""Extracts OSM natural=reef polygons from the Maldives extract into a compact GeoJSON.

OpenMapTiles has no reef layer, but reefs and lagoons are what makes the atolls read as the
Maldives, so the map style draws them from this small GeoJSON (a separate source next to the
PMTiles). Coordinates are rounded to 5 decimals (~1 m) and lightly simplified.
"""
import json
import sys
from osgeo import ogr

pbf, out_path = sys.argv[1], sys.argv[2]
ogr.UseExceptions()
ds = ogr.Open(pbf)
layer = ds.GetLayerByName("multipolygons")
layer.SetAttributeFilter("natural = 'reef' OR other_tags LIKE '%\"natural\"=>\"reef\"%'")

features = []
for feat in layer:
    geom = feat.GetGeometryRef()
    if geom is None or geom.IsEmpty():
        continue
    geom = geom.SimplifyPreserveTopology(0.00004)
    if geom.IsEmpty():
        continue
    parsed = json.loads(geom.ExportToJson(options=["COORDINATE_PRECISION=5"]))
    features.append({"type": "Feature", "properties": {}, "geometry": parsed})

with open(out_path, "w") as f:
    json.dump({"type": "FeatureCollection", "features": features}, f, separators=(",", ":"))
print(f"wrote {out_path}: {len(features)} reef polygons")

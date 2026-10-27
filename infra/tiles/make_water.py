"""Builds a small Maldives-only replacement for the global OSM "water polygons" shapefile.

Planetiler's OpenMapTiles profile expects water-polygons-split-3857 (930 MB, global). For a
Maldives-only map that download is pointless, so we derive ocean polygons from the coastline
ways inside the Geofabrik extract: ocean = bounding box minus every closed coastline ring.
Output: a zipped shapefile in EPSG:3857 that Planetiler reads like the official one, plus
../land.geojson (the islands) for the map style.
"""
import json
import os
import shutil
import sys
import zipfile
from osgeo import ogr, osr

pbf, out_dir = sys.argv[1], sys.argv[2]
ogr.UseExceptions()
src = ogr.Open(pbf)
# GDAL's OSM driver files closed coastline ways under multipolygons (natural=coastline) and
# open ones under lines, so read both.
land = ogr.Geometry(ogr.wkbMultiPolygon)
open_lines = ogr.Geometry(ogr.wkbMultiLineString)

mp = src.GetLayerByName("multipolygons")
mp.SetAttributeFilter("natural = 'coastline'")
for feat in mp:
    geom = feat.GetGeometryRef()
    if geom is not None and geom.GetArea() > 0:
        if geom.GetGeometryType() == ogr.wkbPolygon:
            land.AddGeometry(geom.Clone())
        else:
            for i in range(geom.GetGeometryCount()):
                land.AddGeometry(geom.GetGeometryRef(i).Clone())

lines = src.GetLayerByName("lines")
lines.SetAttributeFilter("other_tags LIKE '%\"natural\"=>\"coastline\"%'")
for feat in lines:
    geom = feat.GetGeometryRef()
    if geom is not None:
        open_lines.AddGeometry(geom.Clone())
print(f"closed coastline polygons: {land.GetGeometryCount()}, open coastline ways: {open_lines.GetGeometryCount()}")
if open_lines.GetGeometryCount():
    for g in open_lines.Polygonize():
        if g.GetArea() > 0:
            land.AddGeometry(g.Clone())

land = land.UnionCascaded()

# The same land polygons double as the map's island layer (drawn above reefs and lagoons).
land_path = os.path.join(os.path.dirname(out_dir.rstrip("/")), "land.geojson")
simplified = land.SimplifyPreserveTopology(0.00001)
land_features = []
pieces = [simplified] if simplified.GetGeometryType() == ogr.wkbPolygon else [simplified.GetGeometryRef(i) for i in range(simplified.GetGeometryCount())]
for piece in pieces:
    if piece.IsEmpty():
        continue
    land_features.append({"type": "Feature", "properties": {}, "geometry": json.loads(piece.ExportToJson(options=["COORDINATE_PRECISION=6"]))})
with open(land_path, "w") as fh:
    json.dump({"type": "FeatureCollection", "features": land_features}, fh, separators=(",", ":"))
print(f"wrote {land_path}: {len(land_features)} land polygons")

west, south, east, north = 72.0, -1.5, 74.2, 7.6
ring = ogr.Geometry(ogr.wkbLinearRing)
for x, y in [(west, south), (east, south), (east, north), (west, north), (west, south)]:
    ring.AddPoint_2D(x, y)
box = ogr.Geometry(ogr.wkbPolygon)
box.AddGeometry(ring)
water = box.Difference(land)

wgs84 = osr.SpatialReference()
wgs84.ImportFromEPSG(4326)
wgs84.SetAxisMappingStrategy(osr.OAMS_TRADITIONAL_GIS_ORDER)
merc = osr.SpatialReference()
merc.ImportFromEPSG(3857)
merc.SetAxisMappingStrategy(osr.OAMS_TRADITIONAL_GIS_ORDER)
water.Transform(osr.CoordinateTransformation(wgs84, merc))

shp_dir = os.path.join(out_dir, "water-polygons-split-3857")
shutil.rmtree(shp_dir, ignore_errors=True)
os.makedirs(shp_dir, exist_ok=True)
driver = ogr.GetDriverByName("ESRI Shapefile")
ds = driver.CreateDataSource(shp_dir)
layer = ds.CreateLayer("water_polygons", merc, ogr.wkbPolygon)
parts = [water] if water.GetGeometryType() == ogr.wkbPolygon else [water.GetGeometryRef(i) for i in range(water.GetGeometryCount())]
for part in parts:
    f = ogr.Feature(layer.GetLayerDefn())
    f.SetGeometry(part)
    layer.CreateFeature(f)
ds = None

zip_path = os.path.join(out_dir, "water-polygons-split-3857.zip")
with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
    for name in os.listdir(shp_dir):
        z.write(os.path.join(shp_dir, name), f"water-polygons-split-3857/{name}")
print(f"wrote {zip_path} ({len(parts)} polygons)")

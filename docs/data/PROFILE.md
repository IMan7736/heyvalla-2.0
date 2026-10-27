# Dataset profile (KANDU, 2026-10-26)

Aggregates only. No raw rows. Source folder `datasets/` is read-only and git-ignored. Counts below were measured by streaming each file once. `DataTools profile` reproduces the ride and MACL figures; the FollowMe and MTCC.xlsx figures came from one-off scripts (FollowMe is also summarised by `build-sea-speeds`).

Extra finding: ride durations differ by under 3% between time-of-day bands for every zone pair, so they look computed from distance rather than measured; do not infer congestion from them. Airport-island zones (Velana, Domestic Terminal, Seaplane) lie within about 1 km, so ride assignment between them is approximate.

## Summary

| Dataset | What it really is | Rows | Period | Usable for |
|---|---|---|---|---|
| `Avas_Ride/avas_rides_aug_sep_2026.csv` | Taxi/ride-hailing trips, Greater Malé only | 371 788 | 2026-08-01 to 2026-09-30 | Taxi duration model, distance-to-time fallback, demand by hour |
| `Avas_Ride/car_fare_matrix.csv` | Fixed taxi fare per zone pair (MVR) | 6 zones x 6 | undated | Taxi fares (real, but only 6 zones) |
| `FollowMe/.../1..31.csv` | GPS pings from boat trackers (about 1 400-1 500 devices per day) | about 440 000-500 000 per day, 31 days (about 1.85 GB) | 2025-08-01 to 2025-08-31 | Sea speeds and route durations, vessel-activity heatmap, terminal location checks |
| `MACL/Airport traffic_Dataset_27092026.csv` | International flight movements and passenger counts at the airport, carriers and routes anonymised | 168 959 | 2012 to 2026-09 (gap in 2020) | Seasonality and demand index only |
| `MTCC.xlsx` | Bus and public-ferry monthly/daily passenger counts | 180 bus-month, 1 095 bus-day, 258 ferry-month rows | 2023-01 to 2025-12 | Route and zone names only. **Numbers are random.** |

## Avas_Ride/avas_rides_aug_sep_2026.csv
- Columns: `date_started` (local timestamp, no offset, assumed Indian/Maldives), `pickup_lat/lng`, `dropoff_lat/lng` (double precision, 11+ digits), `distance_km`, `time_sec`, `status`.
- Status mix: `payment_completed` 320 140, `ride_expired` 27 247, `ride_completed` 24 401. Expired rides have no usable duration and are excluded from duration statistics.
- Distance: 1.98-13.69 km, median about 8.0 km. Time: 300-1 214 s (5-20 min), median about 12 min. Note: `time_sec` has a hard floor of 300 s, so very short trips are clipped. This is a quality flag for any short-trip model.
- Coverage: lat 4.169-4.240, lng 73.500-73.551. That is Malé, Hulhulé, Hulhumalé only. Nothing outside Greater Malé, so no island-to-island or atoll taxi data.
- Join keys: none. No ride id, user id or driver id (good). Join to catalog is spatial (nearest taxi zone).
- Personal-data risk: **high if raw**. Precise pickup and dropoff coordinates with second-level timestamps can identify home/work patterns. Rule: never copy rows. Only aggregates by zone pair x hour-of-day bucket with n >= 10 leave the machine; coordinates are never emitted, only zone ids.
- Powers: duration per zone pair (median, p90), time-of-day congestion factor, linear fallback `duration = a + b * km`. Does **not** contain fares.
- Quality problems: no timezone, clipped durations, no vehicle type, expired rides carry distance/time that are probably estimates.

## Avas_Ride/car_fare_matrix.csv
- 6 x 6 matrix, city "Male City", MVR whole numbers (stored as floats). Zones: Male, Hulhumale Phase 1, Velana International Airport, Hulhumale Phase 2, Sea Plane, Domestic Terminal.
- Diagonal is 30 (a minimum fare inside a zone), off-diagonal 30-120. The matrix is fully symmetric (checked by hand for all 15 pairs); the loader asserts this and fails if a future version is not.
- Powers: taxi fares for every pair among those six zones. Real-looking, source is the operator. Everything outside these zones is an estimate (fallback formula).

## FollowMe (boat GPS)
- Columns: `poll_id`, `imei` (device serial), `timestamp` (server), `device_date`, `latitude`, `longitude`, `speed` (assumed knots, unit not stated: **unverified**), `heading`, `network` (1 Dhiraagu, 2 Ooredoo, 0 unknown), `cellid`, `gsm_level`.
- Day 1: 441 125 rows, 1 432 devices; day 15: 501 388 rows, 1 500 devices. Median about 135-190 pings per device per day, so most devices are sparse; a few are dense.
- Speed: p10 = 0, median about 5, p90 about 29 (max about 97, likely GPS glitches). Heavy-tailed; needs outlier clipping.
- Extent: lat -0.9 to 7.1, lng 72.4 to 74.1: covers the central/north/south atolls, not Addu fully.
- Ping density by `device_date` hour peaks at 02:00-06:00 and 09:00-13:00 with a trough at night. That fits UTC (morning and afternoon in Maldives local time, UTC+5), not local time. Timezone is not stated: **assumed UTC, unverified**.
- Personal-data risk: **medium**. Vessels not people, but IMEIs identify a specific boat and its owner, and a track can reveal a private resort or home. Rule: drop `imei`, `cellid`, `poll_id`; aggregate to grid cells of 0.01 degrees (about 1 km) with at least 10 distinct devices; hour buckets only.
- Powers: typical vessel speed by corridor (speed model for illustrative sea-route durations), activity heatmap. Cannot give timetables: there is no route or service id, and devices are not tied to scheduled ferries.
- Quality problems: unit of speed unstated, no vessel type (cargo, dhoni, speedboat, fishing all mixed), duplicate/lagging pings (`timestamp` vs `device_date` differ), device_date seconds-level jitter.

## MACL airport traffic
- Metadata in `Metadata.csv` (20 columns). Rows are flight movements with `arr_/transit_/dep_` passengers by adults/children/infants, `ata`/`atd` actual times, `region`, anonymised `carrier_code` (113 values), `route_code` (1 008 values), `aircraft_category`.
- Period 2012-01 to 2026-09. About 8-16 k rows per year, dip in 2020 (4 463), growth since 2021.
- Mix: 99% `SCHEDULED`. Regions: South Asia 36%, Middle East 30%, Southeast Asia 15%, Europe 8%, East Asia 7%.
- Empty: `ata` 72, `atd` 71, `carrier_code` 1, `arrival_date` 1.
- No scheduled time column, so **no delay data** can be derived. The dataset is **international** (route and carrier codes anonymised); it contains no domestic flights, so it cannot give domestic timetables.
- Personal-data risk: low (aggregate counts, anonymised).
- Powers: monthly seasonality index of airport demand (used as a crowding prior), airport peak hours by `ata`/`atd`.

## MTCC.xlsx
- Sheets: README, `bus_monthly`, `bus_daily`, `ferry_monthly`. The README states **"all passenger figures are randomly generated and are not actual MTCC / RTL data"**.
- Real, usable: bus route codes and names R1, R2, R3, R4, R7 (Greater Malé); ferry zones and codes: RTL Zone 01 (Haa Alifu: H1, S1C1, S1C5), Zone 02 (Lhaviyani R2C1, R2C2; Noonu Z2R1), Zone 05 (Gaafu Alifu and Gaafu Dhaalu: Z5R2, Z5R5).
- Synthetic: all passenger counts. Never used as demand evidence. Only used as test fixtures and to show how a real feed would look.
- Not present: timetables, fares, stops, route geometry. We therefore cannot take timetables from any provided file: **all timetables in the catalog are synthetic** and flagged as such.

## Join keys across datasets
None are shared. The links are: zone names (taxi matrix <-> place names), geography (rides and boat pings <-> place coordinates), route and zone codes (MTCC <-> catalog routes).

## What the catalog can honestly claim
| Fact | Basis |
|---|---|
| Taxi fares between six Greater Malé zones | `car_fare_matrix.csv` (real) |
| Taxi duration, Greater Malé | Avas rides (real, aggregated) |
| Vessel speed corridors | FollowMe (real, aggregated, unit unverified) |
| Airport seasonality | MACL (real, international only) |
| Route and zone names | MTCC (real names) |
| Ferry, speedboat and domestic-flight timetables and fares | **Synthetic**, shaped to look plausible, flagged `synthetic` |
| Delay and reliability | **Synthetic priors**, flagged `synthetic`; no dataset has scheduled-vs-actual |

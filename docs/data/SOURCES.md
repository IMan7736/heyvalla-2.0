# External data sources: honest evaluation (KANDU, 2026-10-26)

Assessment based on what each portal publishes at the level of its public description. None was bulk-downloaded; nothing here is imported yet. "Useful" means it improves Dhathuru's catalog, not that it is interesting.

| Source | Scope | Useful for Dhathuru? | Licence | Access / refresh | Decision |
|---|---|---|---|---|---|
| OpenStreetMap via Overpass API | Global, good coverage of Maldivian airports and many jetties/ferry terminals; island names patchy (many `name`-less terminals) | **Yes, for coordinates** of airports, ferry terminals and islands | ODbL 1.0: attribution plus share-alike on derived databases | Overpass API, one polite query per refresh, cached; refresh monthly at most | Queried once to sanity-check hand-curated coordinates (airports and Malé/Hulhumalé/Velana/Maafushi terminals looked consistent by eye; no systematic comparison was made, so seed coordinates stay flagged approximate). Not bulk-imported, because share-alike would attach to our catalog and many terminals are unnamed. A future import must attribute OSM contributors and live in a separate, labelled table. |
| data.transportation.gov | United States DOT open data | **No.** US only | Public domain (US gov) | Socrata API | Not used. Context only. |
| transport-data.org portal | Global transport data catalogue (indicators, country-level) | **Marginal**: country-level statistics and methodology, no Maldives timetables | Mixed per dataset | Portal / downloads | Not used for the catalog. Possibly for citations in docs. |
| UN ICAO country-pair air traffic dashboard | International air traffic between country pairs | **Marginal**: international market size and seasonality of arrivals into Maldives; no domestic flights | Terms of use of ICAO / UN; check before redistribution | Dashboard, manual export | Not used. MACL data is far more specific. |
| UNCTADstat | Maritime and trade statistics by country | **No** for trip planning (cargo and fleet statistics) | CC BY 3.0 IGO | Bulk download | Not used. |
| MACL / FollowMe / Avas / MTCC (provided) | Maldives | **Yes**, see PROFILE.md | Provided by Aiman; confirm redistribution terms before any public release | Local files | Used as described in PROFILE.md |
| Operator websites and apps (MTCC, RTL, Manta Air, Maldivian, TMA, Avas) | Real timetables and fares | **Yes in principle** | Terms forbid scraping unless stated | None known as a public API | **Not scraped.** Real data must come through a partner agreement or an official feed; this is what the adapters in ADAPTERS.md are for. |
| Public transport feeds (GTFS) | Maldives | None known to exist | n/a | n/a | Re-check before launch. If an operator publishes GTFS, `StaticTimetableProvider` can import it. |

## Consequence
There is **no licensed source of real Maldivian timetables or fares** in what we have been given or can legally fetch. Therefore:
- Timetables, ferry/speedboat/flight fares and check-in cutoffs are synthetic and flagged `synthetic` / `unverified` in the data and in API responses.
- Taxi fares (zone matrix) and taxi durations (rides) are real.
- UI must show a visible "scheduled/estimated" label wherever `synthetic` is true.

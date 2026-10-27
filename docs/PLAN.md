# Dhathuru: Master Plan (v1, 2026-10-26)

## 1. What we are building
Dhathuru is a door-to-door trip planner and booking app for the whole Maldives. One search plans AND books the entire journey, for example: island ferry -> taxi to the airport island -> domestic flight -> taxi to the final destination. The app picks the best combination automatically and never plans a connection the traveller cannot make.

## 2. Principles (apply to every agent)
1. **Never miss a ride.** Every connection has a hard minimum buffer plus a computed miss-risk. We prefer a slower safe trip over a faster fragile one, and we say so.
2. **Explainable.** Every ranked trip says why (structured explanation codes, translated in the UI).
3. **Replaceable data.** Every external system (ferry, flight, taxi, bus, seaplane, payments, email) sits behind an adapter interface. Static, mock and live implementations are swappable by configuration.
4. **The UI is a product feature.** It must feel premium and unmistakably Maldivian. A large share of the project's value is here.
5. **Quality over speed.** Each agent owns its area end to end: code, tests, docs.
6. **Honest data.** Anything estimated, synthetic or approximate is flagged in code and in the UI. Sea routes are illustrative, never navigational.

## 3. Stack
| Area | Choice |
|---|---|
| Web | Next.js (latest), React 19, TypeScript strict, Tailwind v4, Radix primitives, Motion, TanStack Query, Zod, next-intl, nuqs |
| Map | MapLibre GL JS + PMTiles (self-hosted Maldives vector tiles built with Planetiler) |
| API | .NET 10, ASP.NET Core, EF Core + Npgsql + NetTopologySuite, ASP.NET Core Identity |
| DB | PostgreSQL + PostGIS (Docker locally, Supabase in prod) |
| Contract | OpenAPI split per module, bundled; TS types via openapi-typescript; mock via Prism |
| Tests | Vitest, Playwright, axe, xUnit, FsCheck, Testcontainers |
| Hosting | Vercel (web), Fly.io Singapore (API), Supabase Singapore (DB), R2 or Supabase Storage (tiles; must support HTTP Range) |
| Local infra | Docker Compose: Postgres+PostGIS, Mailpit |

Desktop first: design and test at 1440 wide, minimum supported 1280. Mobile comes later; build with CSS logical properties and fluid units so it is cheap to add. Light theme only.

## 4. Repo layout
```
Heyvalla-2.0/                (GitHub: heyvalla-2.0; the app is called Dhathuru)
  apps/web/                  Next.js app (src/features/*, src/app/* thin routes)
  apps/api/                  .NET solution Dhathuru.sln
    src/Dhathuru.Api          host, endpoint wiring, middleware      (DHONI)
    src/Dhathuru.Domain       shared kernel: ids, money, time, interfaces (DHONI; ATOLL seeds it)
    src/Dhathuru.Identity     accounts, auth                          (DHONI)
    src/Dhathuru.Catalog      places, operators, services, fares      (KANDU)
    src/Dhathuru.Integrations provider adapters (static/mock/live)    (KANDU)
    src/Dhathuru.DataTools    ingestion CLI                           (KANDU)
    src/Dhathuru.TripEngine   pure routing + scoring library          (FARU)
    src/Dhathuru.Bookings     bookings, payments, tickets             (THILA)
    seed/                     small anonymised seed files (<5 MB)
    tests/                    one test project per module
  packages/contracts/        openapi/*.yaml, bundled spec, generated TS
  packages/ui/               design system                           (LAGOON)
  packages/motion/           motion primitives                       (TIDE)
  brand/                     logo-placeholder.svg (real logo.svg later)
  infra/                     docker-compose, Dockerfiles, fly.toml, tile scripts
  datasets/                  raw data. GIT-IGNORED. Read-only.
  docs/                      PLAN, CONTRACT, design/, data/, status/ (ignored), handoffs/ (ignored), agents/ (ignored)
```

## 5. Architecture
```
Browser -> Next.js (Vercel) -- BFF route handlers, httpOnly cookies --> .NET API (Fly)
                                                                          |
   Modules (modular monolith, one DbContext + migration history each):    |
   Identity | Catalog | TripEngine (in-memory snapshot) | Bookings        |
                                                                          v
   Integrations: ITransportProvider (Static | Mock | Live), IPaymentGateway (Fake | future BML/MIB/m-faisaa),
                 IEmailSender (Mailpit dev | Resend later)           Postgres/PostGIS
```
- Modules reference each other only through interfaces in Dhathuru.Domain and by ID (no cross-module foreign keys).
- The BFF keeps tokens out of browser JavaScript and avoids cross-site cookie problems.
- The trip engine works on an in-memory timetable snapshot supplied by Catalog; it never touches the database.

## 6. Agents
| # | Code name | Role | Owns | Dev web port | Dev API port | Dev DB |
|---|---|---|---|---|---|---|
| 1 | ATOLL | Foundation, platform, integration | root config, infra/, packages/contracts (bundling), CI, deploy | 3000 | 5100 | dhathuru_atoll |
| 2 | DHONI | Backend core + accounts | Api, Domain, Identity, web features/account | 3102 | 5102 | dhathuru_dhoni |
| 3 | KANDU | Data and integrations | Catalog, Integrations, DataTools, docs/data | 3103 | 5103 | dhathuru_kandu |
| 4 | FARU | Trip engine (smart planning) | TripEngine (+tests, benchmarks) | n/a | 5104 | dhathuru_faru |
| 5 | THILA | Booking, payments, tickets | Bookings, web features/booking | 3105 | 5105 | dhathuru_thila |
| 6 | LAGOON | Design system and UX direction | packages/ui, web shell, landing, docs/design | 3106 | n/a | n/a |
| 7 | COMPASS | Map experience and tiles | web features/map, infra/tiles | 3107 | n/a | n/a |
| 8 | CURRENT | Planner and trip UI | web features/planner | 3108 | n/a | n/a |
| 9 | TIDE | Motion and interaction polish | packages/motion, web src/motion | 3109 | n/a | n/a |
| 10 | LOOKOUT | QA, security, review (runs last) | tests/e2e, docs/status/LOOKOUT-REPORT.md | 3110 | 5110 | dhathuru_lookout |

Ports 4010 (Prism mock) and Mailpit (1025/8025) and Postgres (5432) are shared.

## 7. Phases
- **Phase 0, setup (Aiman + this chat):** tools installed, kit extracted into the repo, git initialised, `.gitignore` verified, Docker/Supabase/Vercel accounts ready.
- **Phase 1, scaffold gate (ATOLL alone, short):** monorepo, both apps scaffolded and building, contract skeleton, docker-compose, scripts. ATOLL writes `docs/status/PHASE1_DONE` when finished. Nobody else starts before this.
- **Phase 2, parallel build (agents 1-9 simultaneously):** each agent works only inside what it owns. Frontend agents code against the Prism mock; backend agents implement against the contract.
- **Phase 3, integration and polish:** ATOLL switches the web from mock to the real API and fixes seams. DHONI/THILA/KANDU finalise migrations. TIDE and LAGOON do a full route-by-route polish pass (during this phase they may touch any web file, one at a time, announced in docs/status).
- **Phase 4, QA (LOOKOUT):** end-to-end, accessibility, performance, security, privacy and data review. Report with severity-ranked findings; fixes by owners.
- **Phase 5, deploy:** Vercel + Fly + Supabase, tile hosting, smoke tests.

## 8. Cross-cutting conventions
- **i18n:** English only now, built for more. No hard-coded user-facing strings: next-intl message keys everywhere. API returns codes and parameters, never English prose, for explanations and errors. Use CSS logical properties (margin-inline, padding-inline) so RTL (Dhivehi/Thaana) works later. Dates/numbers via Intl with locale.
- **Time:** store and transport UTC (ISO 8601 with offset). Display in Indian/Maldives (UTC+5, no DST). Never use naive local times.
- **Money:** integer minor units (`amountMinor`) plus `currency` ("MVR", 100 laari). Never floats.
- **IDs:** prefixed ULIDs (`isl_`, `apt_`, `svc_`, `trp_`, `bkg_`, `tkt_`, `usr_`).
- **Accessibility:** WCAG 2.2 AA. Check contrast: coral on sand fails for text (accent only); teal on sand is marginal for small text (use teal for buttons, large text, and on white); deep ocean on sand is excellent.
- **Performance:** LCP < 2.0 s on dev build budget, INP < 200 ms, animations on transform/opacity only, 60 fps.
- **Testing:** every module ships tests. No feature is done without them.
- **Docs:** each agent keeps a short `docs/status/<CODENAME>.md` (done / in progress / blocked / decisions).

## 9. Brand
| Colour | Hex | Use |
|---|---|---|
| Lagoon teal | #087F8C | Main buttons, active tabs |
| Warm sand | #FAF7F0 | App background |
| Coconut white | #FFFFFF | Cards and input fields |
| Deep ocean | #12343B | Headings and body text |
| Sunset coral | #F28C78 | Highlights and small accents |

Logo: `brand/logo-placeholder.svg` for now. The app renders it through one `<Logo />` component reading `/brand/logo.svg`; when Aiman supplies the final SVG he drops it in as `brand/logo.svg` and nothing else changes.

## 10. Acceptance demo for v1 (the story every agent builds toward)
A new user signs up, searches island A -> island B for tomorrow ("arrive by" 17:00, 2 passengers). They see ranked options labelled Fastest / Cheapest / Safest / Best overall, each a ferry -> taxi -> flight -> taxi journey with buffers, risk and a plain-language "why". The map animates the chosen route in sync with the timeline. They book all legs in one checkout (fake card), receive per-leg QR tickets and an email (Mailpit), and see the trip under My trips. A simulated delay on one leg triggers a notification and a suggested re-plan that preserves safe connections.

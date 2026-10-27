# Deploying Dhathuru

Targets: **Vercel** (web), **Fly.io, region `sin`** (API), **Supabase, Singapore** (Postgres + PostGIS), **R2 or Supabase Storage** (map tiles). No secret ever goes in git: set them in each provider's dashboard or CLI.

```
Browser -> Vercel (Next.js, BFF route handlers) -> Fly.io (.NET API) -> Supabase Postgres
                         \-> tiles bucket (PMTiles, HTTP Range) <- Browser (MapLibre)
```

## 1. Supabase (database)
1. Create a project in **Southeast Asia (Singapore)**. Save the database password in a password manager.
2. Database > Extensions: enable **postgis**.
3. Two connection strings are needed (Project Settings > Database):
   - **Pooled** (transaction pooler, port 6543): used by the running API.
   - **Direct** (port 5432): used only to apply EF Core migrations.
4. Map them to API settings (the API builds the connection string from `Database__*`):

   | Setting | Runtime (pooled) | Migrations (direct) |
   | --- | --- | --- |
   | `Database__Host` | pooler host | `db.<ref>.supabase.co` |
   | `Database__Port` | `6543` | `5432` |
   | `Database__Name` | `postgres` | `postgres` |
   | `Database__Username` | `postgres.<ref>` | `postgres` |
   | `Database__Password` | secret | secret |

   Each module keeps its own schema (`identity`, `catalog`, `bookings`) and its own `__ef_migrations` table, so one database is enough.
5. Apply migrations from a trusted machine with the direct settings exported:
   `dotnet ef database update --project src/Dhathuru.<Module> --startup-project src/Dhathuru.Api --context <Context>` for each module (Identity: `AccountsDbContext`, Catalog: `CatalogDbContext`, Bookings: `BookingsDbContext`).
6. Enable row level security is NOT needed: the database is never exposed to browsers; only the API connects.

## 2. Fly.io (API)
```bash
fly apps create dhathuru-api --org <org>
fly secrets set -a dhathuru-api \
  Database__Host=<pooler-host> Database__Name=postgres Database__Username=postgres.<ref> Database__Password=<secret>
fly deploy apps/api --config infra/fly.toml -a dhathuru-api
curl https://dhathuru-api.fly.dev/v1/health
```
- Config: `infra/fly.toml` (region `sin`, port 8080, health check `/v1/health`, auto stop/start). `apps/api/Dockerfile` builds from the `apps/api` directory.
- Required API settings outside Development (from DHONI):
  | Setting | Kind | Notes |
  | --- | --- | --- |
  | `Auth__SigningKeyPem` | secret | ES256 PKCS#8 PEM. Generate: `openssl ecparam -name prime256v1 -genkey -noout \| openssl pkcs8 -topk8 -nocrypt` |
  | `Auth__WebBaseUrl` | env | public web URL, used in emailed links |
  | `Email__Provider`, `Email__Host`, `Email__Port` (+ key) | env/secret | Mailpit in dev; Resend later |
  | `Proxy__KnownNetworks__0` | env | already `172.16.0.0/12` in `infra/fly.toml` (Fly private network). Never trust all proxies: it makes `X-Forwarded-For` spoofable and defeats per-client rate limits |
  | `Database__MigrateOnStartup` | env | `false` in production; run the module migrators from a release step instead |
  | `Bookings__Tickets__ActiveKeyId` | env | id of the signing key used for new tickets, e.g. `k1` |
  | `Bookings__Tickets__Keys__k1` | secret | base64, at least 32 random bytes: `openssl rand -base64 32`. Without it the API crashes at startup (`TicketSigner`). To rotate: add `k2`, switch `ActiveKeyId`, keep `k1` until its tickets expire |
  | `Bookings__Verification__OperatorKey` | secret | shared secret operators send as `X-Operator-Key` to consume tickets. Empty disables consumption |
  | `Bookings__EnableSimulation` | env | **must be `false`** in Production (the API refuses to start otherwise). It enables `tok_fake_leg_failure` and the provider-event simulator |
  | `Bookings__WebBaseUrl` | env | public web URL for links in booking emails |
  | `Bff__SharedKey` | secret | same value as `BFF_SHARED_KEY` on Vercel (section 3). Lets the API trust the end-user address the BFF reports |
  | `Catalog__Source` | env | `database` (default). Import the catalog first with the DataTools `import-catalog` command (`bash scripts/api.sh datatools import-catalog`) using the direct DB settings |

- Add every new secret to this table when a module introduces it.
- **Payments:** the API refuses to start in Production while `FakeGateway` is registered (`ProductionSafetyCheck`), and no real gateway exists yet (`docs/payments/GATEWAY-READINESS.md`). Until one does, a Production deploy fails on purpose. For a staging demo with the fake gateway use `ASPNETCORE_ENVIRONMENT=Staging`, keep `Bookings__EnableSimulation=false`, and never take real card data.
- **One machine only.** The planner's option store, the email queue and the repeat-email cooldowns live in memory. `infra/fly.toml` sets `min_machines_running = 1`; after the first deploy run `fly scale count 1 -a dhathuru-api` (a default deploy creates two machines, and a second machine or a restart makes `/trip/<id>` and "Book this trip" return `OPTION_NOT_FOUND` and drops queued emails). Lift this only after those stores are persisted.
- Rate limiting (policies `auth` and `default`), security headers and forwarded-header handling live in the API host (`PlatformServices`, `SecurityHeadersMiddleware`).

## 3. Vercel (web)
1. Import the GitHub repo. **Root Directory: `apps/web`**. Framework: Next.js. Settings come from `apps/web/vercel.json` (installs and builds with pnpm/turbo from the repo root, region `sin1`).
2. Environment variables (Production and Preview):

   | Variable | Value |
   | --- | --- |
   | `API_MODE` | `real` |
   | `API_BASE_URL` | `https://dhathuru-api.fly.dev/v1` |
   | `NEXT_PUBLIC_TILES_URL` | HTTPS URL of the PMTiles archive (section 4). **Build-time**, see below |
   | `BFF_SHARED_KEY` | secret, same value as `Bff__SharedKey` on Fly (`openssl rand -base64 32`) |

   Do not set `API_MOCK_URL` in production.
   - **`NEXT_PUBLIC_TILES_URL` must be in the process environment when `next build` runs** (Vercel project variables, or `NEXT_PUBLIC_TILES_URL=... pnpm build` locally). `next.config.ts` builds the CSP `connect-src` from `process.env` before Next loads `.env.local`, so a value that lives only in `apps/web/.env.local` reaches the client bundle but not the CSP, and the browser blocks the tile requests. Changing it needs a rebuild, not a restart.
   - **Client address path (Vercel to Fly):** the browser reaches a Vercel function, which calls Fly from Vercel's egress addresses, so on its own the API would key rate limits on those shared addresses (everyone in one bucket). The BFF therefore sends the visitor's address (`x-vercel-forwarded-for`, else `x-real-ip`) as `X-Dhathuru-Client-Ip` together with `X-Dhathuru-Bff-Key`. The API uses it only when the key matches `Bff__SharedKey`, and otherwise keys on the connection. `X-Forwarded-For` is honoured only from `Proxy__KnownNetworks__0` (Fly's `172.16.0.0/12` proxy hop). Today only the auth routes do this (`trustedClientIp`); the booking proxy (`features/booking/server/bff.ts:117`) still forwards the first, client-supplied `X-Forwarded-For` entry without the key, so the API ignores it and keys booking calls on the connection (handed off to THILA). Without the shared key on both sides the limiter still works but is shared by all users. **Not yet verified on a real Vercel to Fly deploy:** after deploying, make 11 failed logins from one network and confirm the 11th gets `429`, then confirm a second network is not limited.
3. Deploy; check `/api/health`.

## 4. Tile hosting (PMTiles)
The map reads a single `.pmtiles` file with HTTP Range requests; the host MUST support `Range` and send CORS headers.
- **Cloudflare R2**: create a bucket, upload the file, attach a custom domain or enable the public `r2.dev` URL, then set CORS: allowed origins = the Vercel domains, methods `GET, HEAD`, allowed headers `Range, If-Match`, expose headers `ETag, Content-Range, Content-Length`.
- **Supabase Storage** (alternative): public bucket; verify with `curl -sI -H "Range: bytes=0-99" <url>` that the response is `206 Partial Content`.
- The web CSP allows the tile origin automatically from `NEXT_PUBLIC_TILES_URL` (it must be in the build environment, not only `.env.local`; see section 3).
- Tiles are built by the COMPASS pipeline (`infra/tiles`); the file itself is git-ignored.

## 5. Smoke test after every deploy
```bash
curl -fsS https://dhathuru-api.fly.dev/v1/health          # {"status":"ok",... "database":"up"}
curl -fsS https://<web-domain>/api/health                 # {"status":"ok"}
curl -sI -H "Range: bytes=0-99" "$NEXT_PUBLIC_TILES_URL" | head -1   # HTTP/2 206
```
Then open the site, run one search and open the map.

## 6. Security checklist
- Response headers: web sends CSP, HSTS (prod), `nosniff`, `frame-ancestors 'none'`; the API sends `nosniff`, DENY, `no-referrer`, CSP `default-src 'none'`, `no-store`. Fly terminates TLS and forces HTTPS.
- No CORS on the API (BFF only). Rate limits are configured under `RateLimiting` in the API (defaults in `PlatformServices`).
- Secrets only in Vercel/Fly/Supabase; `infra/.env` and user-secrets are local only.
- Raw dataset rows are never deployed or served; the seed data in `apps/api/seed/` is aggregated or synthetic.
- Rotate the Supabase password and JWT key if a laptop is lost; `fly secrets set` triggers a rolling restart.

## 7. CI
GitHub Actions (`.github/workflows/ci.yml`): web lint/typecheck/test/build, contract drift check, API build/test, Playwright smoke. Deployment itself is manual for now (Vercel Git integration deploys on push; Fly via `fly deploy`).

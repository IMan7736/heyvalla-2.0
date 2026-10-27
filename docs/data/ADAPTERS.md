# Provider adapters (KANDU)

Interface: `ITransportProvider` (Domain). Implementations in `Dhathuru.Integrations/Providers`:
- `StaticTimetableProvider`: catalog timetable only. No seats, status `unknown`, booking calls fail with `PROVIDER_READ_ONLY`.
- `MockTransportProvider`: simulates an operator: deterministic base occupancy, holds with TTL, idempotent hold/confirm/cancel (by key), refunds (100% to 2 h, 50% to 30 min, then 0; synthetic policy), `FailNextConfirm`, and `DisruptionBoard` delays/cancels per run or service.
- `ResilientProvider`: wraps a live adapter: timeout, circuit breaker, 30 s departure cache, reads fall back to static, writes return `PROVIDER_UNAVAILABLE`.

Selection: `Providers:Default:Kind` (default `mock`) and `Providers:{providerId}:Kind` = static|mock|live. `live` without a registered adapter degrades to static with a warning. Resilience options: `Providers:{id}:Resilience:{Timeout,FailureThreshold,OpenFor,DepartureCacheTtl}`.

## Adding a real operator
1. Confirm the operator permits API access (never scrape against terms).
2. Implement `ITransportProvider`; run ids must be stable strings; `HoldAsync`/`ConfirmAsync`/`CancelAsync` must be idempotent per `IdempotencyKey`; map operator errors to failure codes.
3. Register `ILiveProviderFactory` with the catalog operator's `ProviderId`; set `Providers:{id}:Kind=live`.
4. Update catalog service rows (or import an official feed); set `synthetic=false` and `cutoffVerified=true` only for confirmed data.
Status API reports `source`: static, mock, simulated or live (UI shows scheduled vs live).

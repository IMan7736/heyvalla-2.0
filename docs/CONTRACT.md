# Dhathuru: Domain and API Contract (v1)

Source of truth for shapes is `packages/contracts/openapi/*.yaml` (ATOLL seeds it from this document; each module owner edits only their own file). This document is the human-readable intent.

## Conventions
- Base path `/v1`. JSON. Errors are RFC 9457 `application/problem+json` with a stable machine `code` and `params` (no English prose needed by the client).
- Auth: Bearer JWT (access) + rotating refresh token. The web never stores tokens in JS-readable storage (BFF sets httpOnly cookies).
- Pagination: cursor based (`cursor`, `limit`).
- Idempotency: all POSTs that create bookings or payments require an `Idempotency-Key` header.
- Time: ISO 8601 with offset. Money: `{ amountMinor: int, currency: "MVR" }`. IDs: prefixed ULIDs.

## Core domain terms
- **Place**: `island | atoll | airport | jetty | taxi_stand | poi | address`, with `id`, `name` (localisable), `geo`, `atollId?`, `code?` (IATA for airports), `parentId?`. A **Hub** is a place where modes interchange (jetty+airport island).
- **Operator / Provider**: who runs a service and how we reach them (`static | mock | live`).
- **Service**: a scheduled or on-demand run (`ferry | speedboat | seaplane | domestic_flight | bus | taxi | walk | resort_transfer`), with timetable, calendar validity, fare rules.
- **Leg**: one movement. **TripOption**: an ordered list of legs plus scores. **Booking**: one purchase of a TripOption; **BookingLeg** per leg; **Ticket** per bookable leg; **Payment**; **Passenger**; **User**.

## TripOption (engine output)
```
TripOption {
  id, departAt, arriveAt, durationMin, transfers
  legs: Leg[]
  price: { total: Money, breakdown: [{ code, amount: Money, legId? }] }
  buffers: [{ afterLegIndex, minutes, requiredMinutes, comfortMinutes }]
  risk: { missProbability: 0..1, level: "low"|"medium"|"high", weakestConnectionIndex }
  labels: ("fastest"|"cheapest"|"safest"|"best_overall")[]
  score: number
  explanations: [{ code, params, severity: "info"|"good"|"caution" }]
  warnings: [{ code, params }]
}
Leg {
  id, mode, providerId, serviceId?, from: PlaceRef, to: PlaceRef,
  departAt, arriveAt, durationMin, price: Money,
  bookable: boolean, pricing: "fixed"|"estimate", geometryRef?, noteCodes: string[]
}
```
Starter explanation codes: FASTEST_OVERALL, CHEAPEST_OVERALL, SAFEST_BUFFER, BUFFER_COMFORTABLE{minutes}, BUFFER_TIGHT{minutes}, LAST_SERVICE_OF_DAY{mode}, CHECKIN_CUTOFF_OK{minutes}, OVERNIGHT_REQUIRED, FEWER_TRANSFERS{count}, ESTIMATED_TAXI_FARE, DELAY_HISTORY_GOOD{percent}, DELAY_HISTORY_POOR{percent}. Agents may add codes; the web owns the English text for each code.

## Endpoints (v1 surface)
**Meta**: `GET /health`
**Auth/Account (DHONI)**: `POST /auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/verify-email`, `/auth/forgot-password`, `/auth/reset-password`; `GET|PATCH /me`; `GET|POST|PATCH|DELETE /me/travellers`; `DELETE /me` (account deletion).
**Catalog (KANDU)**: `GET /places?query&type&near&limit`, `GET /places/{id}`, `GET /operators`, `GET /services?from&to&date`, `GET /status/services/{id}`.
**Trips (FARU, hosted by Api)**: `POST /trips/search` body `{ from, to, date, time, timeMode: "departAfter"|"arriveBy", passengers, preferences: { maxTransfers?, budgetMax?, modes?, riskTolerance: "cautious"|"balanced"|"relaxed" } }` -> `{ searchId, options: TripOption[] }`; `GET /trips/options/{id}`; `POST /trips/replan` body `{ bookingId, now, disruption }`.
**Geo (COMPASS+KANDU)**: `GET /geo/legs?ids=` -> GeoJSON per leg (flagged `approximate`).
**Bookings (THILA)**: `POST /bookings` (from optionId + passengers) -> hold with expiry; `GET /bookings`, `GET /bookings/{id}`; `POST /bookings/{id}/pay`; `POST /bookings/{id}/cancel`; `GET /bookings/{id}/tickets`; `GET /tickets/{id}/qr`; `POST /tickets/verify`.

## Provider adapter (KANDU defines, implementers follow)
```csharp
public interface ITransportProvider {
  string ProviderId { get; }
  ProviderKind Kind { get; }   // Static | Mock | Live
  Task<IReadOnlyList<ServiceDeparture>> GetDeparturesAsync(DepartureQuery q, CancellationToken ct);
  Task<Availability> GetAvailabilityAsync(AvailabilityQuery q, CancellationToken ct);
  Task<HoldResult> HoldAsync(HoldRequest r, CancellationToken ct);
  Task<ConfirmResult> ConfirmAsync(ConfirmRequest r, CancellationToken ct);
  Task<CancelResult> CancelAsync(CancelRequest r, CancellationToken ct);
  Task<ServiceStatus> GetStatusAsync(string serviceRunId, CancellationToken ct);
}
```
Other seams (in Dhathuru.Domain): `IPaymentGateway` (THILA), `IEmailSender` (DHONI), `ITimetableSnapshotProvider` and `IReliabilityStats` (KANDU -> FARU), `ITripPlanner` (FARU -> Api).

## Booking state machine (THILA)
Draft -> Held (TTL, default 12 min) -> PaymentPending -> Confirmed | Failed | Expired; Confirmed -> PartiallyCancelled | Cancelled | Refunded. Multi-leg confirmation is a saga: if any leg fails to confirm, compensate (release/cancel the others) and refund. Every transition is idempotent and audited.

## Change control
Additive changes to your own module's OpenAPI file are free. Anything touching another module's file, `components/` shared schemas, or a breaking change goes through a handoff note to ATOLL (`docs/handoffs/<FROM>-to-ATOLL.md`).

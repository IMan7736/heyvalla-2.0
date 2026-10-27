# Payment gateway readiness

**Status: no real gateway is integrated.** BML, MIB and Ooredoo m-faisaa are *not* connected. Everything that takes money today is `FakeGateway`, an in-memory simulator that never touches a network. Do not describe it to users or investors as a payment integration.

This document says exactly what has to be built and agreed to replace it. Code lives in `apps/api/src/Dhathuru.Bookings/Payments`.

## 1. The seam

`IPaymentGateway` (Bookings module) has four operations. A real adapter implements all four.

| Method | Contract |
|---|---|
| `ChargeAsync(bookingId, amount, paymentToken, idempotencyKey)` | Authorise and capture in one step. Returns `Succeeded`, `RequiresAction` (with a challenge id) or `Declined` (with a stable failure code). **Must be idempotent on the key**: the same key never charges twice. |
| `CompleteChallengeAsync(gatewayReference, challengeId, code)` | Finish a 3-D Secure / OTP step. |
| `LookupAsync(idempotencyKey)` | Answer "what happened to this charge?" after a timeout or lost response. Returns `null` only if the gateway never saw the request. The reconciler depends on this. |
| `RefundAsync(gatewayReference, amount, idempotencyKey)` | Full or partial. Idempotent on the key. |

Failure codes the rest of the system understands: `CARD_DECLINED`, `INSUFFICIENT_FUNDS`, `VERIFICATION_FAILED`, `PAYMENT_TIMEOUT`, `GATEWAY_ERROR`, `INVALID_PAYMENT_TOKEN`. Map every gateway error to one of these; the web has a message for each.

Register the adapter in `BookingsModule` in place of `FakeGateway`, and add the gateway name to the `Payment.gateway` enum in `components/schemas.yaml` (handoff to ATOLL).

Note: `Dhathuru.Domain` also holds an older `IPaymentGateway` seed. Bookings uses its own richer interface (see `docs/handoffs/THILA-to-ATOLL.md`).

## 2. What the saga guarantees (and what a gateway must not break)

* Seats are held **before** money is taken; money is taken **before** seats are confirmed. If any leg then fails to confirm, the legs are released and a full refund is issued.
* A charge whose response is lost is never assumed to have failed: the booking stays `payment_pending` and the sweeper calls `LookupAsync`. A found success continues the saga; a found decline fails the booking.
* Every provider and gateway call carries a deterministic idempotency key (`{bookingId}:{legIndex}:{op}`, `{bookingId}:pay:{attempt}`, refund keys derived from booking and amount). The gateway **must** honour them.
* The hold TTL (12 min) must exceed the gateway's own session timeout, or a customer can pay for a booking that has already expired. Configure `Bookings:HoldTtl` and `Bookings:PaymentTimeout` together.

## 3. Card data and PCI scope

Card numbers must never reach the Dhathuru API, logs or database. The browser sends the card to the gateway's **hosted fields / redirect / SDK** and receives an opaque token, which is what `POST /bookings/{id}/pay` carries as `paymentToken`.

* Using hosted fields or a redirect keeps the platform at the lightest PCI DSS self-assessment (SAQ A). Anything that posts card data to our own server is a much larger scope and is out of bounds.
* The checkout UI in `apps/web/src/features/booking` today has a card form for the **fake** flow only. It maps test card numbers to fake tokens *in the browser* and sends the token. When a real gateway is added, replace that form with the gateway's hosted component and delete the mapper.

## 4. Per-gateway work (to be confirmed with each provider)

None of the following has been verified against a provider's documentation or sandbox. Treat it as a checklist of questions, not facts.

| | Bank of Maldives (BML) | Maldives Islamic Bank (MIB) | Ooredoo m-faisaa |
|---|---|---|---|
| Needed from them | Merchant account, sandbox credentials, API documentation | Same | Same (wallet, not card) |
| Flow to confirm | Hosted checkout vs. tokenised card API; 3-D Secure support | Same | Wallet redirect / push-to-pay with PIN confirmation |
| Webhooks | Confirm signature scheme, retry policy, source IPs | Same | Same |
| Refunds | Full/partial, time limit, settlement delay | Same | Whether refunds are supported at all |
| Currency | MVR (and USD?) settlement | Same | MVR |

Wallet-style methods (m-faisaa) do not fit "charge a token synchronously": the customer approves on a phone. That maps to `RequiresAction` with `CompleteChallenge`, or needs a new `ChargeOutcome.Pending` plus a webhook handler. That change touches the saga; plan it before promising it.

### Webhooks (not implemented)
The fake gateway needs none. A real one will call back asynchronously. Required work: an unauthenticated `POST /v1/payments/webhooks/{gateway}` endpoint that verifies the provider signature on the raw body, rejects replays (timestamp + event id table), is idempotent, and only moves a payment via the same state machine the synchronous path uses. The sweeper already repairs a missed webhook through `LookupAsync`.

## 5. Operational checklist before real money

* Secrets (merchant ids, API keys, signing keys) in user-secrets / the host's secret store. Never in the repo or logs.
* Separate sandbox and production configuration. Built: `ProductionSafetyCheck` stops the host at startup in Production while `FakeGateway` is the registered `IPaymentGateway` or `Bookings:EnableSimulation` is true. A real gateway must be registered before the module (the module only adds the fake when none is present). Tested; not yet run against a full Production secret set.
* `Bookings:EnableSimulation` **false** in production. It enables `/dev/provider-events` and the `tok_fake_leg_failure` fault injection.
* Daily reconciliation report: gateway settlement file vs. `bookings.payments` and `refunds`. Not built.
* Legal: refund policy text, terms, receipts and tax invoices as required in the Maldives (GST). Not built.
* Chargeback handling. Not built.

## 6. The fake gateway

Deterministic, selected by token. The web checkout translates these test cards to tokens; the API only ever sees the token.

| Test card | Token | Behaviour |
|---|---|---|
| 4242 4242 4242 4242 | `tok_fake_success` | Succeeds |
| 4000 0000 0000 0002 | `tok_fake_declined` | Declined (`CARD_DECLINED`); booking stays held so another card can be tried |
| 4000 0000 0000 9995 | `tok_fake_insufficient_funds` | Declined (`INSUFFICIENT_FUNDS`) |
| 4000 0000 0000 3220 | `tok_fake_verification` | Needs verification; code **123456** |
| 4000 0000 0000 0341 | `tok_fake_leg_failure` | Charge succeeds, then a leg fails to confirm: full compensation and refund (only when `Bookings:EnableSimulation` is true) |
| 4000 0000 0000 0119 | `tok_fake_timeout` | No answer in time; reconciler finds nothing charged and fails the booking |
| (token only) | `tok_fake_timeout_charged` | No answer in time, but the charge went through; reconciler completes the booking |
| (token only) | `tok_fake_slow` | Succeeds after `Bookings:FakeGateway:SlowDelay` |

Any other card number or token is rejected as `INVALID_PAYMENT_TOKEN`.

## 7. Third-party components and licences

* **QR codes**: written in-house (`Tickets/QrEncoder.cs`, ISO/IEC 18004 byte mode, error-correction M, versions 1 to 10). No library, no licence obligations. Output was checked with the independent decoder `zbarimg`.
* **PDF itinerary**: written in-house (`Services/ItineraryPdf.cs`: A4, Helvetica, QR drawn as vector squares). No library. Text is limited to the standard Latin set; a Dhivehi (Thaana) itinerary would need font embedding, which is not built.
* No NuGet package was added by the Bookings module.

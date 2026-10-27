import type { Route } from "@playwright/test";

/**
 * Stateful stand-in for the booking BFF (`/book/api/*`), following bookings.yaml. Lets each branch of the flow
 * (decline, verification, leg failure, expiry, refund) run deterministically without an API.
 */
export const BOOKING_ID = "bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2";

const place = (id: string, name: string) => ({
  id,
  type: "island",
  name: { en: name },
  geo: { lat: 4, lng: 73 },
});
const money = (amountMinor: number) => ({ amountMinor, currency: "MVR" });
const iso = (offsetMs: number) => new Date(Date.now() + offsetMs).toISOString();

function leg(
  index: number,
  mode: string,
  from: string,
  to: string,
  hours: number,
  fare: number,
  bookable = true,
) {
  return {
    id: `leg_01K8Z3M5Q7R9T2V4W6X8Y0G1H${index}`,
    mode,
    providerId: "static-example",
    from: place(`isl_01K8Z3M5Q7R9T2V4W6X8Y0A1B${index}`, from),
    to: place(`isl_01K8Z3M5Q7R9T2V4W6X8Y0A1C${index}`, to),
    departAt: iso((hours + 24) * 3_600_000),
    arriveAt: iso((hours + 25) * 3_600_000),
    durationMin: 60,
    price: money(fare),
    bookable,
    pricing: "fixed",
    noteCodes: [],
  };
}

export class FakeBookingApi {
  booking: Record<string, unknown> | null = null;
  requests: { method: string; path: string; headers: Record<string, string>; body: unknown }[] = [];
  holdMs = 12 * 60_000;
  private expiresAt = 0;
  private legs = [
    leg(0, "ferry", "Maafushi", "Hulhumalé", 2, 9000),
    leg(1, "seaplane", "Hulhumalé", "Kooddoo", 5, 85000),
    leg(2, "taxi", "Kooddoo", "Resort", 7, 3000, false),
  ];

  option() {
    return {
      id: "trp_01K8Z3M5Q7R9T2V4W6X8Y0E1F2",
      legs: this.legs,
      price: { total: money(94000), breakdown: [] },
      buffers: [],
      departAt: this.legs[0]?.departAt,
    };
  }

  private base(status: string, extra: Record<string, unknown> = {}) {
    const legStatus =
      status === "confirmed"
        ? "confirmed"
        : status === "cancelled"
          ? "cancelled"
          : status === "failed" || status === "expired"
            ? "released"
            : "held";
    return {
      id: BOOKING_ID,
      status,
      optionId: "trp_01K8Z3M5Q7R9T2V4W6X8Y0E1F2",
      legs: this.legs.map((l, i) => ({
        id: `bkl_01K8Z3M5Q7R9T2V4W6X8Y0J1K${i}`,
        legIndex: i,
        status: l.bookable ? legStatus : "pending",
        providerReference: status === "confirmed" && l.bookable ? `SB-48291${i}` : null,
        failureCode: null,
        amount: money(l.bookable ? l.price.amountMinor : 0),
        leg: l,
      })),
      passengers: [
        {
          id: "pax_01K8Z3M5Q7R9T2V4W6X8Y0Q1R2",
          fullName: "Aishath Naseem",
          passengerType: "adult",
        },
      ],
      total: money(94500),
      holdExpiresAt:
        status === "held" || status === "payment_pending"
          ? new Date(this.expiresAt).toISOString()
          : null,
      payment: null,
      createdAt: iso(-60_000),
      updatedAt: iso(0),
      failureCode: null,
      priceBreakdown: [
        { code: "FARE", amount: money(9000), legIndex: 0, payOnBoard: false },
        { code: "FARE", amount: money(85000), legIndex: 1, payOnBoard: false },
        { code: "SERVICE_FEE", amount: money(500), payOnBoard: false },
        { code: "PAY_ON_BOARD", amount: money(3000), legIndex: 2, payOnBoard: true },
      ],
      disruptions: [],
      ...extra,
    };
  }

  private payment(status: string, extra: Record<string, unknown> = {}) {
    return {
      id: "pay_01K8Z3M5Q7R9T2V4W6X8Y0M1N2",
      bookingId: BOOKING_ID,
      amount: money(94500),
      status,
      gateway: "fake",
      createdAt: iso(-30_000),
      ...extra,
    };
  }

  /** Current state, expiring the hold when its time has come. */
  current() {
    if (this.booking && this.booking.status === "held" && Date.now() >= this.expiresAt)
      this.booking = this.base("expired");
    return this.booking;
  }

  async handle(route: Route) {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(/^\/book\/api\//, "");
    const method = request.method();
    let body: unknown = null;
    try {
      body = request.postDataJSON();
    } catch {
      body = null;
    }
    this.requests.push({ method, path, headers: request.headers(), body });
    const json = (data: unknown, status = 200) => route.fulfill({ status, json: data });

    if (method === "GET" && path.startsWith("options/")) return json(this.option());
    if (method === "POST" && path === "bookings") {
      this.expiresAt = Date.now() + this.holdMs;
      this.booking = this.base("held");
      return json(this.booking, 201);
    }
    if (method === "GET" && path === "bookings")
      return json({
        items: this.current() ? [this.current()] : [],
        page: { nextCursor: null, limit: 50 },
      });
    if (method === "GET" && path === `bookings/${BOOKING_ID}`)
      return this.current() ? json(this.current()) : json({ code: "NOT_FOUND" }, 404);
    if (method === "POST" && path === `bookings/${BOOKING_ID}/pay`) {
      if (Date.now() >= this.expiresAt) {
        this.booking = this.base("expired");
        return json({ code: "HOLD_EXPIRED" }, 410);
      }
      const token = (body as { paymentToken?: string } | null)?.paymentToken;
      if (token === "tok_fake_declined") {
        this.booking = this.base("held", {
          payment: this.payment("failed", { failureCode: "CARD_DECLINED" }),
        });
      } else if (token === "tok_fake_verification") {
        this.booking = this.base("payment_pending", {
          payment: this.payment("requires_action", {
            challenge: { id: "chl_1", kind: "verification_code" },
          }),
        });
      } else if (token === "tok_fake_leg_failure") {
        this.booking = this.base("failed", {
          failureCode: "LEG_CONFIRM_FAILED",
          payment: this.payment("refunded", { refunded: money(94500) }),
        });
      } else {
        this.booking = this.base("confirmed", { payment: this.payment("succeeded") });
      }
      return json(this.booking);
    }
    if (method === "POST" && path === `bookings/${BOOKING_ID}/pay/verify`) {
      if ((body as { code?: string } | null)?.code === "123456") {
        this.booking = this.base("confirmed", { payment: this.payment("succeeded") });
        return json(this.booking);
      }
      return json({ code: "VALIDATION_FAILED" }, 422);
    }
    if (method === "GET" && path === `bookings/${BOOKING_ID}/tickets`) {
      const items = this.legs.flatMap((l, i) =>
        l.bookable
          ? [
              {
                id: `tkt_01K8Z3M5Q7R9T2V4W6X8Y0K1M${i}`,
                bookingId: BOOKING_ID,
                bookingLegId: `bkl_01K8Z3M5Q7R9T2V4W6X8Y0J1K${i}`,
                passengerId: "pax_01K8Z3M5Q7R9T2V4W6X8Y0Q1R2",
                status: this.booking?.status === "cancelled" ? "cancelled" : "valid",
                issuedAt: iso(-1000),
                leg: l,
              },
            ]
          : [],
      );
      return json({ items });
    }
    if (method === "GET" && path.startsWith("tickets/") && path.endsWith("/qr")) {
      return route.fulfill({
        status: 200,
        contentType: "image/svg+xml",
        body: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 21 21"><rect width="21" height="21" fill="#fff"/><rect x="2" y="2" width="5" height="5"/></svg>',
      });
    }
    if (method === "GET" && path === `bookings/${BOOKING_ID}/cancellation-quote`) {
      return json({
        bookingId: BOOKING_ID,
        cancellable: true,
        legs: [
          {
            legIndex: 0,
            cancellable: true,
            policyCode: "ferry-standard",
            refundPercent: 100,
            refund: money(9000),
          },
          {
            legIndex: 1,
            cancellable: true,
            policyCode: "seaplane-standard",
            refundPercent: 50,
            refund: money(42500),
          },
        ],
        totalRefund: money(51500),
        nonRefundable: money(42500),
      });
    }
    if (method === "POST" && path === `bookings/${BOOKING_ID}/cancel`) {
      this.booking = this.base("cancelled", {
        payment: this.payment("partially_refunded", { refunded: money(51500) }),
      });
      return json(this.booking);
    }
    if (method === "GET" && path.endsWith("itinerary.pdf"))
      return route.fulfill({ status: 200, contentType: "application/pdf", body: "%PDF-1.4" });
    return json({ code: "NOT_FOUND" }, 404);
  }
}

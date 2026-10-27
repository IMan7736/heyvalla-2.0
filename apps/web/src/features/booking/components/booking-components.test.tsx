import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { bookingFixture } from "../test/fixtures";
import { renderBooking } from "../test/render";
import { CardForm } from "./card-form";
import { Checkout } from "./checkout";
import { HoldCountdown } from "./hold-countdown";
import { PriceBreakdown } from "./price-breakdown";
import { TripsList } from "./trips-list";

const replace = vi.fn();
const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ replace, push }) }));

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
  replace.mockReset();
  push.mockReset();
});

describe("CardForm", () => {
  it("validates locally and never lets a bad card reach onPay", async () => {
    const onPay = vi.fn();
    renderBooking(<CardForm amountLabel="MVR 90" busy={false} onPay={onPay} />);
    await userEvent.click(screen.getByRole("button", { name: /Pay MVR 90/ }));
    expect(await screen.findByText("Enter the name on the card.")).toBeInTheDocument();
    expect(screen.getByText("Enter the card number.")).toBeInTheDocument();
    expect(onPay).not.toHaveBeenCalled();
  });

  it("sends only the gateway token, never the card number, and wipes the number", async () => {
    const onPay = vi.fn();
    renderBooking(<CardForm amountLabel="MVR 90" busy={false} onPay={onPay} />);
    await userEvent.type(screen.getByLabelText(/Name on card/), "Aishath Naseem");
    await userEvent.type(screen.getByLabelText(/Card number/), "4000000000000002");
    await userEvent.type(screen.getByLabelText(/Expiry/), "1231");
    await userEvent.type(screen.getByLabelText(/Security code/), "123");
    await userEvent.click(screen.getByRole("button", { name: /Pay MVR 90/ }));
    expect(onPay).toHaveBeenCalledExactlyOnceWith("tok_fake_declined");
    expect(JSON.stringify(onPay.mock.calls)).not.toContain("4000");
    expect(screen.getByLabelText(/Card number/)).toHaveValue("");
    expect(screen.getByLabelText(/Security code/)).toHaveValue("");
  });

  it("says plainly that a non-test card isn't accepted", async () => {
    renderBooking(<CardForm amountLabel="MVR 90" busy={false} onPay={vi.fn()} />);
    await userEvent.type(screen.getByLabelText(/Name on card/), "A B");
    await userEvent.type(screen.getByLabelText(/Card number/), "5555555555554444");
    await userEvent.type(screen.getByLabelText(/Expiry/), "1231");
    await userEvent.type(screen.getByLabelText(/Security code/), "123");
    await userEvent.click(screen.getByRole("button", { name: /Pay/ }));
    expect(await screen.findByText(/only the listed test cards work/)).toBeInTheDocument();
  });

  it("fills a test card from the list", async () => {
    renderBooking(<CardForm amountLabel="MVR 90" busy={false} onPay={vi.fn()} />);
    await userEvent.click(screen.getByRole("button", { name: "Show test cards" }));
    const row = screen.getByText("Asks for a verification code").closest("li");
    expect(row).not.toBeNull();
    await userEvent.click(
      within(row as HTMLElement).getByRole("button", { name: "Use this card" }),
    );
    expect(screen.getByLabelText(/Card number/)).toHaveValue("4000 0000 0000 3220");
  });
});

describe("HoldCountdown", () => {
  beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: false }));

  it("counts down, turns urgent, and tells the owner once at zero", () => {
    vi.setSystemTime(new Date("2026-10-26T10:00:00Z"));
    const onExpired = vi.fn();
    renderBooking(<HoldCountdown expiresAt="2026-10-26T10:01:45Z" onExpired={onExpired} />);
    expect(screen.getByText("01:45")).toBeInTheDocument();
    expect(screen.getByText(/Hurry/)).toBeInTheDocument();
    act(() => {
      vi.advanceTimersByTime(104_000);
    });
    expect(screen.getByText("00:01")).toBeInTheDocument();
    expect(onExpired).not.toHaveBeenCalled();
    act(() => {
      vi.advanceTimersByTime(3_000);
    });
    expect(screen.getByText("00:00")).toBeInTheDocument();
    expect(onExpired).toHaveBeenCalledTimes(1);
  });
});

describe("PriceBreakdown", () => {
  it("lists charges, the total, and pay-on-board separately", () => {
    renderBooking(
      <PriceBreakdown
        total={{ amountMinor: 9500, currency: "MVR" }}
        lines={[
          {
            code: "FARE",
            amount: { amountMinor: 9000, currency: "MVR" },
            legIndex: 0,
            payOnBoard: false,
          },
          { code: "SERVICE_FEE", amount: { amountMinor: 500, currency: "MVR" }, payOnBoard: false },
          {
            code: "PAY_ON_BOARD",
            amount: { amountMinor: 2000, currency: "MVR" },
            legIndex: 1,
            payOnBoard: true,
          },
        ]}
      />,
    );
    expect(screen.getByText("Fare · leg 1")).toBeInTheDocument();
    expect(screen.getByText("Service fee")).toBeInTheDocument();
    expect(screen.getByText("MVR 95")).toBeInTheDocument();
    expect(screen.getByText("Pay on board: MVR 20")).toBeInTheDocument();
    expect(screen.queryByText("Pay on board (not charged here)")).not.toBeInTheDocument();
  });
});

describe("Checkout", () => {
  it("pays with the token, an idempotency key, and goes to the confirmation on success", async () => {
    const confirmed = bookingFixture({ status: "confirmed" });
    const calls: { url: string; init?: RequestInit }[] = [];
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
      const url = String(input);
      calls.push({ url, ...(init ? { init } : {}) });
      if (url.endsWith("/pay")) return json(confirmed);
      return json(bookingFixture());
    });
    renderBooking(<Checkout bookingId="bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2" />);
    await userEvent.type(await screen.findByLabelText(/Name on card/), "Aishath Naseem");
    await userEvent.type(screen.getByLabelText(/Card number/), "4242424242424242");
    await userEvent.type(screen.getByLabelText(/Expiry/), "1231");
    await userEvent.type(screen.getByLabelText(/Security code/), "123");
    await userEvent.click(screen.getByRole("button", { name: /Pay MVR 90/ }));
    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith("/book/done/bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2"),
    );
    const pay = calls.find((c) => c.url.endsWith("/pay"));
    expect(pay?.init?.method).toBe("POST");
    expect(JSON.parse(String(pay?.init?.body))).toEqual({
      method: "card",
      paymentToken: "tok_fake_success",
    });
    expect((pay?.init?.headers as Record<string, string>)["Idempotency-Key"]).toBeTruthy();
    expect(JSON.stringify(calls)).not.toContain("4242");
  });

  it("keeps the seats and offers another card after a decline", async () => {
    const declined = bookingFixture({
      payment: {
        id: "pay_1",
        bookingId: "bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2",
        amount: { amountMinor: 9000, currency: "MVR" },
        status: "failed",
        gateway: "fake",
        failureCode: "CARD_DECLINED",
        createdAt: "2026-10-26T20:01:00Z",
      },
    });
    vi.spyOn(globalThis, "fetch").mockImplementation(async () => json(declined));
    renderBooking(<Checkout bookingId="bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2" />);
    expect(await screen.findByText("Payment didn’t go through")).toBeInTheDocument();
    expect(screen.getByText(/The card was declined/)).toBeInTheDocument();
    expect(screen.getByText(/Your seats are still held/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Card number/)).toBeInTheDocument();
  });

  it("asks for the verification code when the bank wants one", async () => {
    const challenge = bookingFixture({
      status: "payment_pending",
      payment: {
        id: "pay_1",
        bookingId: "bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2",
        amount: { amountMinor: 9000, currency: "MVR" },
        status: "requires_action",
        gateway: "fake",
        createdAt: "2026-10-26T20:01:00Z",
        challenge: { id: "chl_1", kind: "verification_code" },
      },
    });
    vi.spyOn(globalThis, "fetch").mockImplementation(async () => json(challenge));
    renderBooking(<Checkout bookingId="bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2" />);
    expect(await screen.findByRole("heading", { name: "Verify this payment" })).toBeInTheDocument();
    expect(screen.getByText(/the code is 123456/)).toBeInTheDocument();
  });

  it("explains an expired hold: seats released, nothing charged", async () => {
    vi.spyOn(globalThis, "fetch").mockImplementation(async () =>
      json(bookingFixture({ status: "expired" })),
    );
    renderBooking(<Checkout bookingId="bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2" />);
    expect(await screen.findByText("Your hold ran out")).toBeInTheDocument();
    expect(screen.getByText(/you were not charged/)).toBeInTheDocument();
  });

  it("tells the user the money is refunded when a leg could not be confirmed", async () => {
    const failed = bookingFixture({
      status: "failed",
      failureCode: "LEG_CONFIRM_FAILED",
      payment: {
        id: "pay_1",
        bookingId: "bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2",
        amount: { amountMinor: 9000, currency: "MVR" },
        status: "refunded",
        gateway: "fake",
        createdAt: "2026-10-26T20:01:00Z",
      },
    });
    vi.spyOn(globalThis, "fetch").mockImplementation(async () => json(failed));
    renderBooking(<Checkout bookingId="bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2" />);
    expect(await screen.findByText("We couldn’t complete this booking")).toBeInTheDocument();
    expect(screen.getByText(/full amount is being refunded/)).toBeInTheDocument();
    expect(screen.getByText(/seats we held have been released/)).toBeInTheDocument();
  });

  it("shows an error state with retry when the booking can't be loaded", async () => {
    vi.spyOn(globalThis, "fetch").mockImplementation(async () => json({ code: "UNKNOWN" }, 500));
    renderBooking(<Checkout bookingId="bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2" />);
    expect(
      await screen.findByText("We couldn’t load this booking", undefined, { timeout: 4000 }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Try again" })).toBeInTheDocument();
  });
});

describe("TripsList", () => {
  it("shows the empty state with a next step", async () => {
    vi.spyOn(globalThis, "fetch").mockImplementation(async () =>
      json({ items: [], page: { nextCursor: null, limit: 50 } }),
    );
    renderBooking(<TripsList />);
    expect(await screen.findByText("No upcoming trips")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Plan a trip" })).toHaveAttribute("href", "/plan");
  });

  it("lists trips with status chips and a live disruption banner, and splits upcoming from past", async () => {
    const future = bookingFixture({
      status: "confirmed",
      disruptions: [
        {
          id: "dsr_1",
          bookingLegId: "bkl_01K8Z3M5Q7R9T2V4W6X8Y0J1K2",
          legIndex: 0,
          kind: "delayed",
          delayMinutes: 45,
          state: "open",
          detectedAt: "2026-10-26T20:00:00Z",
          suggestion: null,
        },
      ],
    });
    const pastLeg = bookingFixture().legs[0];
    const past = bookingFixture({
      id: "bkg_01K8Z3M5Q7R9T2V4W6X8Y0P4S7",
      status: "confirmed",
      legs: pastLeg
        ? [
            {
              ...pastLeg,
              leg: {
                ...pastLeg.leg,
                departAt: "2026-10-01T02:30:00Z",
                arriveAt: "2026-10-01T04:00:00Z",
              },
            },
          ]
        : [],
    });
    vi.spyOn(globalThis, "fetch").mockImplementation(async () =>
      json({ items: [future, past], page: { nextCursor: null, limit: 50 } }),
    );
    renderBooking(<TripsList />);
    expect(await screen.findByText("Maafushi → Hulhumalé Ferry Terminal")).toBeInTheDocument();
    expect(screen.getAllByText("Confirmed")).toHaveLength(1);
    expect(screen.getByText("Leg 1 is delayed by 45 min")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("radio", { name: "Past" }));
    expect(await screen.findByText("Maafushi → Hulhumalé Ferry Terminal")).toBeInTheDocument();
    expect(screen.queryByText("Leg 1 is delayed by 45 min")).not.toBeInTheDocument();
  });

  it("shows an error state with retry", async () => {
    vi.spyOn(globalThis, "fetch").mockImplementation(async () => json({ code: "UNKNOWN" }, 500));
    renderBooking(<TripsList />);
    expect(
      await screen.findByText("We couldn’t load your trips", undefined, { timeout: 4000 }),
    ).toBeInTheDocument();
  });
});

import { describe, expect, it } from "vitest";
import {
  TEST_CARDS,
  cvcValid,
  expiryValid,
  formatCardNumber,
  formatExpiry,
  luhnValid,
  tokenForCard,
  validateCard,
} from "./cards";
import { URGENT_SECONDS, formatCountdown, secondsLeft } from "./hold";
import {
  estimateFares,
  hasErrors,
  newRow,
  parseParty,
  rowsForParty,
  toInputs,
  validateRows,
} from "./passengers";
import { failureKey, isUpcoming, paymentSettled, shortRef, sortTrips } from "./status";
import { parseSuggestion } from "./suggestion";
import type { Booking } from "./types";

const NOW = new Date("2026-10-26T10:00:00Z");

describe("cards", () => {
  it("every test card is Luhn-valid and maps to a token", () => {
    for (const card of TEST_CARDS) {
      expect(luhnValid(card.number)).toBe(true);
      expect(tokenForCard(card.number)).toBe(card.token);
    }
  });

  it("maps the documented scenarios to the documented tokens", () => {
    expect(tokenForCard("4242 4242 4242 4242")).toBe("tok_fake_success");
    expect(tokenForCard("4000000000000002")).toBe("tok_fake_declined");
    expect(tokenForCard("4000 0000 0000 9995")).toBe("tok_fake_insufficient_funds");
    expect(tokenForCard("4000000000003220")).toBe("tok_fake_verification");
    expect(tokenForCard("4000000000000341")).toBe("tok_fake_leg_failure");
    expect(tokenForCard("4000000000000119")).toBe("tok_fake_timeout");
  });

  it("rejects numbers that are not test cards and bad checksums", () => {
    expect(tokenForCard("5555555555554444")).toBeNull();
    expect(luhnValid("4242424242424241")).toBe(false);
    expect(luhnValid("1234")).toBe(false);
  });

  it("formats while typing", () => {
    expect(formatCardNumber("4242424242424242")).toBe("4242 4242 4242 4242");
    expect(formatCardNumber("42a4")).toBe("424");
    expect(formatExpiry("1228")).toBe("12/28");
    expect(formatExpiry("1")).toBe("1");
  });

  it("checks expiry against the current month", () => {
    expect(expiryValid("10/26", NOW)).toBe(true);
    expect(expiryValid("09/26", NOW)).toBe(false);
    expect(expiryValid("13/30", NOW)).toBe(false);
    expect(expiryValid("1", NOW)).toBe(false);
  });

  it("checks the security code", () => {
    expect(cvcValid("123")).toBe(true);
    expect(cvcValid("1234")).toBe(true);
    expect(cvcValid("12")).toBe(false);
    expect(cvcValid("12a")).toBe(false);
  });

  it("reports field errors", () => {
    expect(validateCard({ name: "", number: "", expiry: "", cvc: "" }, NOW)).toEqual({
      name: "required",
      number: "required",
      expiry: "required",
      cvc: "required",
    });
    expect(
      validateCard(
        { name: "A B", number: "5555 5555 5555 4444", expiry: "12/30", cvc: "123" },
        NOW,
      ),
    ).toEqual({ number: "notTestCard" });
    expect(
      validateCard(
        { name: "A B", number: "4242 4242 4242 4242", expiry: "12/30", cvc: "123" },
        NOW,
      ),
    ).toEqual({});
  });
});

describe("hold countdown", () => {
  it("never goes negative and formats mm:ss", () => {
    expect(secondsLeft("2026-10-26T10:12:00Z", NOW.getTime())).toBe(720);
    expect(secondsLeft("2026-10-26T09:00:00Z", NOW.getTime())).toBe(0);
    expect(secondsLeft(null, NOW.getTime())).toBe(0);
    expect(formatCountdown(720)).toBe("12:00");
    expect(formatCountdown(65)).toBe("01:05");
    expect(URGENT_SECONDS).toBe(120);
  });
});

describe("passengers", () => {
  const adult = (name = "Aishath Naseem") => newRow({ fullName: name });

  it("needs a name, a sane phone and at least one adult", () => {
    expect(hasErrors(validateRows([adult()]))).toBe(false);
    expect(validateRows([newRow()]).rows[0]?.fullName).toBe("nameRequired");
    expect(validateRows([adult(), newRow({ fullName: "X", phone: "abc" })]).rows[1]?.phone).toBe(
      "phoneInvalid",
    );
    expect(validateRows([newRow({ fullName: "Kid", passengerType: "child" })]).party).toBe(
      "adultRequired",
    );
    expect(
      validateRows([
        adult(),
        newRow({ fullName: "A", passengerType: "infant" }),
        newRow({ fullName: "B", passengerType: "infant" }),
      ]).party,
    ).toBe("tooManyInfants");
  });

  it("builds the request without empty optionals", () => {
    const rows = [
      newRow({ fullName: "  Aishath  ", travellerId: "trv_X" }),
      newRow({ fullName: "Ibrahim", phone: " +9607771234 " }),
    ];
    expect(toInputs(rows)).toEqual([
      { travellerId: "trv_X", fullName: "Aishath", passengerType: "adult" },
      { fullName: "Ibrahim", passengerType: "adult", phone: "+9607771234" },
    ]);
  });

  it("estimates from the planner's quote for the group, skipping pay-on-board legs", () => {
    const legs = [
      { bookable: true, price: { amountMinor: 9000 } },
      { bookable: false, price: { amountMinor: 5000 } },
      { bookable: true, price: { amountMinor: 85000 } },
    ];
    expect(estimateFares(legs)).toBe(94000);
  });

  it("fixes the group to the searched party", () => {
    expect(
      rowsForParty({ adults: 2, children: 1, infants: 1 }).map((r) => r.passengerType),
    ).toEqual(["adult", "adult", "child", "infant"]);
    expect(parseParty({ adults: "2", children: "1", infants: "1" })).toEqual({
      adults: 2,
      children: 1,
      infants: 1,
    });
    expect(parseParty({ adults: "1" })).toEqual({ adults: 1, children: 0, infants: 0 });
    expect(parseParty({})).toBeNull();
    expect(parseParty({ adults: "0" })).toBeNull();
    expect(parseParty({ adults: "1", infants: "2" })).toBeNull();
    expect(parseParty({ adults: "9", children: "1" })).toBeNull();
    expect(parseParty({ adults: "x" })).toBeNull();
  });
});

function booking(patch: Partial<Booking>): Booking {
  return {
    id: "bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2",
    status: "confirmed",
    optionId: "trp_X",
    legs: [],
    passengers: [],
    total: { amountMinor: 0, currency: "MVR" },
    createdAt: "2026-10-20T00:00:00Z",
    updatedAt: "2026-10-20T00:00:00Z",
    priceBreakdown: [],
    disruptions: [],
    ...patch,
  } as Booking;
}

function legAt(departAt: string, arriveAt: string) {
  return {
    id: `bkl_${departAt}`,
    legIndex: 0,
    status: "confirmed",
    amount: { amountMinor: 1, currency: "MVR" },
    leg: { departAt, arriveAt },
  } as unknown as Booking["legs"][number];
}

describe("status helpers", () => {
  it("maps failure codes to known keys with a safe fallback", () => {
    expect(failureKey("CARD_DECLINED")).toBe("CARD_DECLINED");
    expect(failureKey("SOMETHING_NEW")).toBe("UNKNOWN");
    expect(failureKey(null)).toBe("UNKNOWN");
  });

  it("splits upcoming from past", () => {
    const now = NOW.getTime();
    const future = booking({ legs: [legAt("2026-10-27T02:00:00Z", "2026-10-27T04:00:00Z")] });
    const past = booking({ legs: [legAt("2026-10-20T02:00:00Z", "2026-10-20T04:00:00Z")] });
    expect(isUpcoming(future, now)).toBe(true);
    expect(isUpcoming(past, now)).toBe(false);
    expect(isUpcoming(booking({ status: "held", legs: past.legs }), now)).toBe(true);
    expect(isUpcoming(booking({ status: "cancelled", legs: future.legs }), now)).toBe(false);
    expect(isUpcoming(booking({ status: "expired", legs: future.legs }), now)).toBe(false);
  });

  it("sorts upcoming soonest first and past latest first", () => {
    const a = booking({
      id: "bkg_A",
      legs: [legAt("2026-10-28T00:00:00Z", "2026-10-28T01:00:00Z")],
    });
    const b = booking({
      id: "bkg_B",
      legs: [legAt("2026-10-27T00:00:00Z", "2026-10-27T01:00:00Z")],
    });
    expect(sortTrips([a, b], true).map((x) => x.id)).toEqual(["bkg_B", "bkg_A"]);
    expect(sortTrips([a, b], false).map((x) => x.id)).toEqual(["bkg_A", "bkg_B"]);
  });

  it("shortens references to the last eight characters", () => {
    expect(shortRef("bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2")).toBe("X8Y0H1J2");
  });

  it("knows when an uncertain payment has been settled", () => {
    expect(paymentSettled(undefined, null)).toBe(false);
    expect(paymentSettled(booking({ status: "confirmed" }), null)).toBe(true);
    expect(paymentSettled(booking({ status: "payment_pending" }), null)).toBe(false);
    const oldFail = { id: "pay_old", status: "failed" } as Booking["payment"];
    const newFail = { id: "pay_new", status: "failed" } as Booking["payment"];
    expect(paymentSettled(booking({ status: "held", payment: oldFail }), "pay_old")).toBe(false);
    expect(paymentSettled(booking({ status: "held", payment: newFail }), "pay_old")).toBe(true);
  });
});

describe("suggestions", () => {
  it("reads well-formed options and ignores junk", () => {
    const good = {
      optionId: "trp_1",
      departAt: "2026-10-27T05:00:00Z",
      arriveAt: "2026-10-27T07:00:00Z",
      total: { amountMinor: 1000, currency: "MVR" },
    };
    expect(
      parseSuggestion({
        requiresConsent: true,
        options: [
          good,
          { optionId: 5 },
          null,
          { ...good, total: { amountMinor: "1", currency: "MVR" } },
        ],
      }),
    ).toEqual([good]);
    expect(parseSuggestion(null)).toEqual([]);
    expect(parseSuggestion("x")).toEqual([]);
    expect(parseSuggestion({ options: "no" })).toEqual([]);
  });
});

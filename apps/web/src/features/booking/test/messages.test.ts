import { readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import booking from "../../../../messages/en/booking.json";
import planner from "../../../../messages/en/planner.json";

const ROOT = path.resolve(__dirname, "..");
const messages: Record<string, unknown> = { booking, planner };

function files(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const full = path.join(dir, name);
    if (statSync(full).isDirectory()) return name === "test" ? [] : files(full);
    return /\.tsx?$/.test(name) && !/\.test\./.test(name) ? [full] : [];
  });
}

function lookup(key: string): unknown {
  return key
    .split(".")
    .reduce<unknown>(
      (node, part) =>
        typeof node === "object" && node !== null
          ? (node as Record<string, unknown>)[part]
          : undefined,
      messages,
    );
}

describe("booking messages", () => {
  it("defines every literal key the booking components use", () => {
    const missing: string[] = [];
    for (const file of files(ROOT)) {
      // Each top-level function is checked on its own: `t` means a different namespace in each component.
      for (const segment of readFileSync(file, "utf8").split(/\n(?=(?:export )?function )/)) {
        const translators = [...segment.matchAll(/const (\w+) = useTranslations\("([\w.]+)"\)/g)];
        for (const [, name, namespace] of translators) {
          for (const call of segment.matchAll(new RegExp(`\\b${name}\\("([\\w.]+)"`, "g"))) {
            if (lookup(`${namespace}.${call[1]}`) === undefined)
              missing.push(`${path.relative(ROOT, file)}: ${namespace}.${call[1]}`);
          }
        }
      }
    }
    expect(missing).toEqual([]);
  });

  it("covers every dynamic key family", () => {
    const families: [string, string[]][] = [
      [
        "booking.status",
        [
          "draft",
          "held",
          "payment_pending",
          "confirmed",
          "failed",
          "expired",
          "partially_cancelled",
          "cancelled",
          "refunded",
        ],
      ],
      ["booking.legStatus", ["pending", "held", "confirmed", "failed", "cancelled", "released"]],
      ["booking.start.types", ["adult", "child", "infant"]],
      [
        "booking.start.problems",
        [
          "LEG_UNAVAILABLE",
          "OPTION_EXPIRED",
          "OPTION_NOT_FOUND",
          "NOTHING_TO_BOOK",
          "VALIDATION_FAILED",
          "EMAIL_NOT_VERIFIED",
          "FORBIDDEN",
          "AUTH_REQUIRED",
          "NETWORK",
          "UNKNOWN",
        ],
      ],
      [
        "booking.start.errors",
        ["nameRequired", "nameTooLong", "phoneInvalid", "adultRequired", "tooManyInfants"],
      ],
      [
        "booking.checkout.problems",
        [
          "HOLD_EXPIRED",
          "PAYMENT_IN_PROGRESS",
          "NO_VERIFICATION_PENDING",
          "BOOKING_NOT_PAYABLE",
          "INVALID_PAYMENT_TOKEN",
          "EMAIL_NOT_VERIFIED",
          "FORBIDDEN",
          "AUTH_REQUIRED",
          "NETWORK",
          "UNKNOWN",
        ],
      ],
      [
        "booking.checkout.card.errors",
        [
          "nameRequired",
          "numberRequired",
          "numberInvalid",
          "numberNotTestCard",
          "expiryRequired",
          "expiryInvalid",
          "cvcRequired",
          "cvcInvalid",
        ],
      ],
      [
        "booking.checkout.testMode.cards",
        ["success", "declined", "insufficientFunds", "verification", "legFailure", "timeout"],
      ],
      [
        "booking.failure",
        [
          "CARD_DECLINED",
          "INSUFFICIENT_FUNDS",
          "VERIFICATION_FAILED",
          "PAYMENT_TIMEOUT",
          "GATEWAY_ERROR",
          "INVALID_PAYMENT_TOKEN",
          "LEG_UNAVAILABLE",
          "LEG_CONFIRM_FAILED",
          "HOLD_EXPIRED",
          "UNKNOWN",
        ],
      ],
      [
        "booking.price",
        [
          "FARE",
          "SERVICE_FEE",
          "PAY_ON_BOARD",
          "FERRY_FARE",
          "SPEEDBOAT_FARE",
          "SEAPLANE_FARE",
          "FLIGHT_FARE",
          "BUS_FARE",
          "TAXI_FARE",
          "RESORT_TRANSFER_FARE",
          "other",
        ],
      ],
      [
        "booking.detail.paymentStatus",
        ["pending", "requires_action", "succeeded", "failed", "refunded", "partially_refunded"],
      ],
      [
        "booking.cancel.problems",
        [
          "CANCEL_FAILED",
          "CANCEL_IN_PROGRESS",
          "BOOKING_NOT_CANCELLABLE",
          "NOTHING_CANCELLABLE",
          "NETWORK",
          "UNKNOWN",
        ],
      ],
      ["booking.wallet.ticketStatus", ["valid", "used", "cancelled", "expired"]],
    ];
    const missing = families.flatMap(([ns, keys]) =>
      keys.filter((k) => lookup(`${ns}.${k}`) === undefined).map((k) => `${ns}.${k}`),
    );
    expect(missing).toEqual([]);
  });
});

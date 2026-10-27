"use client";

import { formatMoney } from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import type { Money, PriceLine } from "../lib/types";

const KNOWN = new Set([
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
]);

/** Honest price table: every charge on its own line, pay-on-board legs listed but clearly not charged. */
export function PriceBreakdown({
  lines,
  total,
  totalLabel,
}: {
  lines: PriceLine[];
  total: Money;
  totalLabel?: string;
}) {
  const t = useTranslations("booking.price");
  const charged = lines.filter((l) => !l.payOnBoard);
  const onBoard = lines.filter((l) => l.payOnBoard);
  const onBoardSum = onBoard.reduce((sum, l) => sum + l.amount.amountMinor, 0);
  const label = (line: PriceLine) => {
    const base = KNOWN.has(line.code) ? t(line.code as "FARE") : t("other");
    return line.legIndex === null || line.legIndex === undefined
      ? base
      : `${base} · ${t("legSuffix", { number: line.legIndex + 1 })}`;
  };
  return (
    <section aria-label={t("title")}>
      <dl className="space-y-2 text-small">
        {charged.map((line, i) => (
          <div
            key={`${line.code}-${line.legIndex ?? "x"}-${i}`}
            className="flex items-baseline justify-between gap-4"
          >
            <dt className="text-ink-muted">{label(line)}</dt>
            <dd className="tabular text-ink">{formatMoney(line.amount)}</dd>
          </div>
        ))}
        <div className="flex items-baseline justify-between gap-4 border-t border-line pt-3 text-body font-semibold">
          <dt className="text-ink">{totalLabel ?? t("total")}</dt>
          <dd className="tabular text-ink">{formatMoney(total)}</dd>
        </div>
      </dl>
      {onBoard.length > 0 ? (
        <p className="mt-3 rounded-md bg-sand-200 px-3 py-2 text-caption text-ink-muted">
          {t("payOnBoardTotal", {
            amount: formatMoney({ amountMinor: onBoardSum, currency: total.currency }),
          })}
        </p>
      ) : null}
    </section>
  );
}

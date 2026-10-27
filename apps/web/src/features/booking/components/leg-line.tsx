"use client";

import { ModeChip, formatDay, formatMoney, formatTime } from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import type { Leg } from "../lib/types";

/** One leg of a journey: mode, where, when. Times are Maldives time. */
export function LegLine({
  leg,
  number,
  trailing,
}: {
  leg: Leg;
  number: number;
  trailing?: React.ReactNode;
}) {
  const tModes = useTranslations("planner.modes");
  const t = useTranslations("booking");
  return (
    <div className="flex items-start gap-4">
      <ModeChip mode={leg.mode} size={40} label={tModes(leg.mode)} />
      <div className="min-w-0 flex-1">
        <p className="text-body font-medium text-ink">
          <span className="sr-only">{t("wallet.legHeading", { number })}. </span>
          {tModes(leg.mode)}
        </p>
        <p className="mt-0.5 text-body text-ink">
          <span className="tabular font-medium">{formatTime(leg.departAt)}</span> {leg.from.name.en}
          <span aria-hidden="true"> → </span>
          <span className="sr-only"> {t("detail.legTo")} </span>
          <span className="tabular font-medium">{formatTime(leg.arriveAt)}</span> {leg.to.name.en}
        </p>
        <p className="tabular text-caption text-ink-muted">{formatDay(leg.departAt)}</p>
      </div>
      {trailing ?? (
        <span className="tabular text-small font-medium text-ink">
          {leg.bookable ? formatMoney(leg.price) : t("start.summary.payOnBoard")}
        </span>
      )}
    </div>
  );
}

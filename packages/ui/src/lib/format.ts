export const MALDIVES_TZ = "Indian/Maldives";

/**
 * Locale used for Intl. Plain "en" resolves to US ordering (Oct 27); Maldivian English reads
 * day-first (27 Oct), so "en" maps to "en-GB". Other locales pass through unchanged.
 */
export function intlLocale(locale: string): string {
  return locale === "en" ? "en-GB" : locale;
}

export type Money = { amountMinor: number; currency: string };

/** Money is integer minor units (laari). 12550 MVR minor => "MVR 125.50". */
export function formatMoney(money: Money, locale = "en", opts: { compact?: boolean } = {}): string {
  const fractionDigits = money.amountMinor % 100 === 0 || opts.compact ? 0 : 2;
  return new Intl.NumberFormat(intlLocale(locale), {
    style: "currency",
    currency: money.currency,
    currencyDisplay: "code",
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  })
    .format(money.amountMinor / 100)
    .replace(/ /g, " ");
}

/** 24-hour clock time in Maldives time from an ISO 8601 instant. */
export function formatTime(iso: string, locale = "en"): string {
  return new Intl.DateTimeFormat(intlLocale(locale), {
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
    timeZone: MALDIVES_TZ,
  }).format(new Date(iso));
}

/** "Tue 27 Oct" in Maldives time. */
export function formatDay(iso: string, locale = "en"): string {
  return new Intl.DateTimeFormat(intlLocale(locale), {
    weekday: "short",
    day: "numeric",
    month: "short",
    timeZone: MALDIVES_TZ,
  }).format(new Date(iso));
}

/** Duration in minutes as "2 h 05 min" via unit formatting (no English prose baked in). */
export function formatDuration(totalMinutes: number, locale = "en"): string {
  const h = Math.floor(totalMinutes / 60);
  const m = Math.round(totalMinutes % 60);
  const unit = (value: number, unitName: "hour" | "minute") =>
    new Intl.NumberFormat(intlLocale(locale), {
      style: "unit",
      unit: unitName,
      unitDisplay: "narrow",
    }).format(value);
  if (h === 0) return unit(m, "minute");
  if (m === 0) return unit(h, "hour");
  return `${unit(h, "hour")} ${unit(m, "minute")}`;
}

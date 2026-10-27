import { useTranslations } from "next-intl";

/**
 * The Dhathuru brand mark, read from /brand/logo.svg. Aiman replaces brand/logo.svg and
 * apps/web/public/brand/logo.svg is refreshed from it; nothing else changes. `variant="full"`
 * adds the wordmark (set in the display serif; the mark alone carries the accessible name).
 */
export function Logo({
  size = 40,
  variant = "mark",
  tone = "default",
}: {
  size?: number;
  variant?: "mark" | "full";
  tone?: "default" | "inverse";
}) {
  const t = useTranslations("common");
  const mark = (
    // eslint-disable-next-line @next/next/no-img-element -- static SVG brand asset, no optimisation needed
    <img src="/brand/logo.svg" alt={t("logoAlt")} width={size} height={size} />
  );
  if (variant === "mark") return mark;
  return (
    <span className="inline-flex items-center gap-3">
      {mark}
      <span
        aria-hidden="true"
        className={`font-display text-title leading-none ${tone === "inverse" ? "text-sand-50" : "text-ink"}`}
      >
        {t("appName")}
      </span>
    </span>
  );
}

"use client";

import { Icon } from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import { useEffect, useRef, useState } from "react";
import { URGENT_SECONDS, formatCountdown, secondsLeft } from "../lib/hold";

/**
 * Counts down to the hold expiry. The server decides what expiry means: at zero we only call `onExpired`,
 * which refetches the booking, rather than assuming the seats are gone.
 */
export function HoldCountdown({
  expiresAt,
  onExpired,
}: {
  expiresAt: string;
  onExpired: () => void;
}) {
  const t = useTranslations("booking.checkout.countdown");
  const [now, setNow] = useState(() => Date.now());
  const left = secondsLeft(expiresAt, now);
  const fired = useRef(false);
  const callback = useRef(onExpired);

  useEffect(() => {
    callback.current = onExpired;
  });
  useEffect(() => {
    fired.current = false;
  }, [expiresAt]);
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);
  useEffect(() => {
    if (left === 0 && !fired.current) {
      fired.current = true;
      callback.current();
    }
  }, [left]);

  const urgent = left <= URGENT_SECONDS;
  // Announce on whole minutes only so screen readers are not flooded every second.
  const announce = t("announce", { minutes: Math.floor(left / 60), seconds: left % 60 });
  return (
    <div className={`rounded-lg p-4 ${urgent ? "bg-caution-soft" : "bg-lagoon-50"}`}>
      <div className="flex items-center gap-3">
        <Icon name="clock" size={22} className={urgent ? "text-caution" : "text-lagoon-600"} />
        <div>
          <p className="text-caption font-medium tracking-wide text-ink-muted uppercase">
            {t("label")}
          </p>
          <p
            className="tabular font-display text-headline leading-none text-ink"
            aria-hidden="true"
          >
            {formatCountdown(left)}
          </p>
        </div>
      </div>
      <p className="mt-2 text-caption text-ink-muted">{urgent ? t("urgent") : t("normal")}</p>
      <span className="sr-only" role="timer" aria-live="off">
        {announce}
      </span>
      <span className="sr-only" role="status" aria-live="polite">
        {left > 0 && left % 60 === 0 ? announce : urgent && left === URGENT_SECONDS ? announce : ""}
      </span>
    </div>
  );
}

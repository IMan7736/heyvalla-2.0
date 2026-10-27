"use client";

import { Alert, Button, formatMoney, formatTime } from "@dhathuru/ui";
import { useMutation } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { bookingApi } from "../lib/api";
import { useStoreBooking } from "../lib/hooks";
import { parseSuggestion } from "../lib/suggestion";
import type { Booking, BookingDisruption } from "../lib/types";

/** Live disruption notices for one booking. We never rebook: alternatives are offered, the traveller chooses. */
export function DisruptionBanners({
  booking,
  compact = false,
}: {
  booking: Booking;
  compact?: boolean;
}) {
  const t = useTranslations("booking.disruption");
  const open = booking.disruptions.filter((d) => d.state !== "resolved");
  if (open.length === 0) return null;
  return (
    <section aria-label={t("region")} className="space-y-3">
      {open.map((d) => (
        <DisruptionItem key={d.id} bookingId={booking.id} disruption={d} compact={compact} />
      ))}
    </section>
  );
}

function DisruptionItem({
  bookingId,
  disruption,
  compact,
}: {
  bookingId: string;
  disruption: BookingDisruption;
  compact: boolean;
}) {
  const t = useTranslations("booking.disruption");
  const store = useStoreBooking();
  const ack = useMutation({
    mutationFn: () => bookingApi.acknowledge(bookingId, disruption.id),
    onSuccess: store,
  });
  const cancelled = disruption.kind === "cancelled";
  const options = parseSuggestion(disruption.suggestion);
  const acknowledged = disruption.state === "acknowledged";
  return (
    <Alert
      tone={cancelled ? "danger" : "caution"}
      title={
        cancelled
          ? t("cancelled", { leg: disruption.legIndex + 1 })
          : t("delayed", { leg: disruption.legIndex + 1, minutes: disruption.delayMinutes })
      }
      action={
        compact || acknowledged ? null : (
          <Button
            size="sm"
            variant="secondary"
            loading={ack.isPending}
            onClick={() => ack.mutate()}
          >
            {ack.isPending ? t("acknowledging") : t("acknowledge")}
          </Button>
        )
      }
    >
      {compact ? null : (
        <div className="space-y-3">
          <p>{cancelled ? t("cancelledBody") : t("delayedBody")}</p>
          {ack.isError ? (
            <p role="alert" className="text-danger">
              {t("ackError")}
            </p>
          ) : null}
          {acknowledged ? <p className="text-ink-muted">{t("acknowledged")}</p> : null}
          <div>
            <p className="font-medium">{t("suggestions")}</p>
            {options.length === 0 ? (
              <p className="mt-1 text-ink-muted">{t("suggestionsNone")}</p>
            ) : (
              <ul className="mt-1 space-y-1">
                {options.map((o) => (
                  <li key={o.optionId} className="flex flex-wrap items-center gap-x-3">
                    <span className="tabular">
                      {t("optionLine", {
                        depart: formatTime(o.departAt),
                        arrive: formatTime(o.arriveAt),
                        total: formatMoney(o.total),
                      })}
                    </span>
                    <Link
                      href={`/trip/${encodeURIComponent(o.optionId)}`}
                      className="text-link underline underline-offset-4"
                    >
                      {t("viewOption")}
                    </Link>
                  </li>
                ))}
              </ul>
            )}
            <p className="mt-2 text-caption text-ink-muted">{t("consent")}</p>
          </div>
        </div>
      )}
    </Alert>
  );
}

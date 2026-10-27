"use client";

import { Alert, Button, Dialog, Skeleton, formatMoney } from "@dhathuru/ui";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { BookingApiError, bookingApi } from "../lib/api";
import { bookingKeys, useStoreBooking } from "../lib/hooks";
import type { Booking } from "../lib/types";

const KNOWN = new Set([
  "CANCEL_FAILED",
  "CANCEL_IN_PROGRESS",
  "BOOKING_NOT_CANCELLABLE",
  "NOTHING_CANCELLABLE",
  "NETWORK",
]);

/** Shows exactly what a cancellation refunds before it happens, then performs it. */
export function CancelDialog({
  booking,
  open,
  onOpenChange,
}: {
  booking: Booking;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const t = useTranslations("booking.cancel");
  const tLeg = useTranslations("planner.modes");
  const store = useStoreBooking();
  const [done, setDone] = useState<Booking | null>(null);
  const quote = useQuery({
    queryKey: bookingKeys.quote(booking.id),
    queryFn: ({ signal }) => bookingApi.quote(booking.id, signal),
    enabled: open && done === null,
    staleTime: 0,
    gcTime: 0,
  });
  const cancel = useMutation({
    mutationFn: () => bookingApi.cancel(booking.id),
    onSuccess: (updated) => {
      store(updated);
      setDone(updated);
    },
  });

  const q = quote.data;
  const refund = q?.totalRefund;
  const problem =
    cancel.error instanceof BookingApiError
      ? KNOWN.has(cancel.error.code)
        ? cancel.error.code
        : "UNKNOWN"
      : cancel.isError
        ? "UNKNOWN"
        : null;
  const refundedLabel = done?.payment?.refunded
    ? formatMoney(done.payment.refunded)
    : refund
      ? formatMoney(refund)
      : null;

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (cancel.isPending) return;
        onOpenChange(next);
        if (!next) cancel.reset();
      }}
      title={done ? t("done.title") : t("title")}
      closeLabel={t("close")}
      size="md"
      footer={
        done ? (
          <Button onClick={() => onOpenChange(false)}>{t("done.close")}</Button>
        ) : (
          <>
            <Button
              variant="secondary"
              onClick={() => onOpenChange(false)}
              disabled={cancel.isPending}
            >
              {t("keep")}
            </Button>
            <Button
              variant="danger"
              loading={cancel.isPending}
              disabled={!q || !q.cancellable}
              onClick={() => cancel.mutate()}
            >
              {cancel.isPending
                ? t("cancelling")
                : refund && refund.amountMinor > 0
                  ? t("confirm", { amount: formatMoney(refund) })
                  : t("confirmNoRefund")}
            </Button>
          </>
        )
      }
    >
      {done ? (
        <div className="space-y-3" role="status">
          <p className="text-body text-ink">
            {refundedLabel && done.payment?.refunded
              ? t("done.refund", { amount: refundedLabel })
              : t("done.noRefund")}
          </p>
          <p className="text-small text-ink-muted">{t("refundTiming")}</p>
        </div>
      ) : quote.isPending ? (
        <div role="status" aria-busy="true" aria-label={t("loadingQuote")} className="space-y-3">
          <Skeleton className="h-6 w-full" />
          <Skeleton className="h-6 w-3/4" />
          <Skeleton className="h-10 w-full" />
        </div>
      ) : quote.isError ? (
        <Alert
          tone="danger"
          action={
            <Button size="sm" variant="secondary" onClick={() => void quote.refetch()}>
              {t("quoteRetry")}
            </Button>
          }
        >
          {t("quoteError")}
        </Alert>
      ) : q ? (
        <div className="space-y-4">
          {!q.cancellable ? <Alert tone="caution">{t("nothing")}</Alert> : null}
          <ul className="divide-y divide-line rounded-md shadow-[var(--shadow-hairline)]">
            {q.legs.map((l) => {
              const leg = booking.legs.find((b) => b.legIndex === l.legIndex)?.leg;
              return (
                <li
                  key={l.legIndex}
                  className="flex items-center justify-between gap-4 p-3 text-small"
                >
                  <span className="min-w-0">
                    <span className="block font-medium text-ink">
                      {t("perLeg", {
                        leg: l.legIndex + 1,
                        route: leg
                          ? `${tLeg(leg.mode)}: ${leg.from.name.en} → ${leg.to.name.en}`
                          : "",
                      })}
                    </span>
                    <span className="block text-caption text-ink-muted">
                      {l.cancellable
                        ? t("refundPercent", { percent: l.refundPercent })
                        : t("notCancellable")}
                    </span>
                  </span>
                  <span className="tabular font-medium text-ink">{formatMoney(l.refund)}</span>
                </li>
              );
            })}
          </ul>
          <dl className="space-y-1 text-small">
            <div className="flex justify-between gap-4 text-body font-semibold">
              <dt>{t("totalRefund")}</dt>
              <dd className="tabular">{formatMoney(q.totalRefund)}</dd>
            </div>
            {q.nonRefundable.amountMinor > 0 ? (
              <div className="flex justify-between gap-4 text-ink-muted">
                <dt>{t("nonRefundable")}</dt>
                <dd className="tabular">{formatMoney(q.nonRefundable)}</dd>
              </div>
            ) : null}
          </dl>
          <p className="text-small text-ink-muted">{t("consequences")}</p>
          <p className="text-small text-ink-muted">{t("refundTiming")}</p>
          {problem ? (
            <Alert tone="danger">{t(`problems.${problem}` as "problems.UNKNOWN")}</Alert>
          ) : null}
        </div>
      ) : null}
    </Dialog>
  );
}

"use client";

import { Alert, Button, ErrorState, EmptyState } from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { failureKey } from "../lib/status";
import type { Booking } from "../lib/types";

/** Terminal states of a booking that was never completed. Each says what happened to the money and the seats. */
export function BookingOutcome({ booking }: { booking: Booking }) {
  const t = useTranslations("booking.checkout");
  const tFail = useTranslations("booking.failure");
  if (booking.status === "expired") {
    return (
      <EmptyState
        title={t("expired.title")}
        description={t("expired.description")}
        action={
          <Button asChild>
            <Link href="/plan">{t("expired.action")}</Link>
          </Button>
        }
      />
    );
  }
  if (booking.status === "failed") {
    return (
      <div>
        <ErrorState
          title={t("failed.title")}
          description={tFail(failureKey(booking.failureCode) as "UNKNOWN")}
          action={
            <Button asChild>
              <Link href="/plan">{t("failed.action")}</Link>
            </Button>
          }
          secondaryAction={
            <Button variant="secondary" asChild>
              <Link href="/trips">{t("cancelled.action")}</Link>
            </Button>
          }
        />
        <FailureConsequences booking={booking} />
      </div>
    );
  }
  return (
    <EmptyState
      title={t("cancelled.title")}
      action={
        <Button asChild>
          <Link href="/trips">{t("cancelled.action")}</Link>
        </Button>
      }
    />
  );
}

/** Money and seat consequences of a failed booking, shown under the failure message. */
export function FailureConsequences({ booking }: { booking: Booking }) {
  const t = useTranslations("booking.checkout.failed");
  const paid =
    booking.payment && booking.payment.status !== "failed" && booking.payment.status !== "pending";
  return (
    <div className="mx-auto max-w-md space-y-2 text-small text-ink-muted">
      <Alert tone={paid ? "caution" : "success"}>{paid ? t("refund") : t("noCharge")}</Alert>
      <p>{t("seatsReleased")}</p>
    </div>
  );
}

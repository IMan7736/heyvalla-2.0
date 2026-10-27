"use client";

import {
  Alert,
  Button,
  Card,
  CardTitle,
  ErrorState,
  Icon,
  Skeleton,
  formatMoney,
} from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { BookingApiError, bookingApi } from "../lib/api";
import { useBooking } from "../lib/hooks";
import { failureKey, routeSummary, shortRef } from "../lib/status";
import type { Booking } from "../lib/types";
import { BookingProviders } from "./booking-providers";
import { CancelDialog } from "./cancel-dialog";
import { DisruptionBanners } from "./disruption-banners";
import { LegLine } from "./leg-line";
import { PriceBreakdown } from "./price-breakdown";
import { StatusChip } from "./status-chip";

export function TripBookingDetail({ bookingId }: { bookingId: string }) {
  return (
    <BookingProviders>
      <DetailInner bookingId={bookingId} />
    </BookingProviders>
  );
}

function DetailInner({ bookingId }: { bookingId: string }) {
  const t = useTranslations("booking.detail");
  const booking = useBooking(bookingId);
  return (
    <div className="mx-auto max-w-[1184px] px-8 pb-24 pt-8">
      <Button variant="ghost" size="sm" asChild className="mb-4">
        <Link href="/trips">
          <Icon name="arrow-left" size={16} /> {t("back")}
        </Link>
      </Button>
      {booking.isPending ? (
        <div role="status" aria-busy="true" aria-label={t("loading")} className="space-y-4">
          <Skeleton className="h-10 w-96" />
          <Skeleton className="h-64 w-full" />
        </div>
      ) : booking.isError ? (
        booking.error instanceof BookingApiError && booking.error.status === 404 ? (
          <ErrorState
            title={t("notFound.title")}
            description={t("notFound.description")}
            action={
              <Button asChild>
                <Link href="/trips">{t("notFound.action")}</Link>
              </Button>
            }
          />
        ) : (
          <ErrorState
            title={t("error.title")}
            description={t("error.description")}
            action={<Button onClick={() => void booking.refetch()}>{t("error.retry")}</Button>}
          />
        )
      ) : (
        <DetailBody booking={booking.data} />
      )}
    </div>
  );
}

function DetailBody({ booking }: { booking: Booking }) {
  const t = useTranslations("booking.detail");
  const tFail = useTranslations("booking.failure");
  const tPay = useTranslations("booking.detail.paymentStatus");
  const tType = useTranslations("booking.start.types");
  const tStatus = useTranslations("booking.legStatus");
  const [cancelOpen, setCancelOpen] = useState(false);
  const route = routeSummary(booking.legs);
  const cancellable = booking.status === "confirmed" || booking.status === "partially_cancelled";
  const hasTickets = ["confirmed", "partially_cancelled", "cancelled", "refunded"].includes(
    booking.status,
  );
  const payment = booking.payment;

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="font-display text-display text-ink">
            {route ? `${route.from} → ${route.to}` : t("pageTitle")}
          </h1>
          <p className="tabular mt-1 text-small text-ink-muted">
            {t("reference", { id: shortRef(booking.id) })}
          </p>
        </div>
        <StatusChip status={booking.status} />
      </header>

      <DisruptionBanners booking={booking} />

      {booking.status === "failed" ? (
        <Alert tone="danger">{tFail(failureKey(booking.failureCode) as "UNKNOWN")}</Alert>
      ) : null}

      <div className="flex flex-wrap gap-3">
        {booking.status === "held" ? (
          <Button asChild>
            <Link href={`/book/pay/${encodeURIComponent(booking.id)}`}>{t("actions.pay")}</Link>
          </Button>
        ) : null}
        {hasTickets ? (
          <>
            <Button asChild>
              <Link href={`/trips/${encodeURIComponent(booking.id)}/tickets`}>
                <Icon name="ticket" size={18} /> {t("actions.tickets")}
              </Link>
            </Button>
            <Button variant="secondary" asChild>
              <a href={bookingApi.pdfUrl(booking.id)} download>
                <Icon name="download" size={18} /> {t("actions.pdf")}
              </a>
            </Button>
          </>
        ) : null}
        {cancellable ? (
          <Button variant="secondary" onClick={() => setCancelOpen(true)}>
            {t("actions.cancel")}
          </Button>
        ) : null}
        {booking.status === "failed" ||
        booking.status === "expired" ||
        booking.status === "cancelled" ||
        booking.status === "refunded" ? (
          <Button variant="secondary" asChild>
            <Link href="/plan">{t("actions.search")}</Link>
          </Button>
        ) : null}
      </div>
      {!hasTickets &&
      booking.status !== "held" &&
      booking.status !== "failed" &&
      booking.status !== "expired" ? (
        <p className="text-small text-ink-muted">{t("ticketsHint")}</p>
      ) : null}

      <div className="grid grid-cols-[minmax(0,1fr)_22rem] items-start gap-8">
        <div className="space-y-6">
          <Card elevation="rest" padding="md">
            <CardTitle className="mb-4">{t("legs")}</CardTitle>
            <ol className="space-y-5">
              {booking.legs.map((l) => (
                <li key={l.id} className="rounded-md p-1">
                  <LegLine
                    leg={l.leg}
                    number={l.legIndex + 1}
                    trailing={
                      <span className="text-end">
                        <span className="block text-small font-medium text-ink">
                          {tStatus(l.status)}
                        </span>
                        {l.providerReference ? (
                          <span className="tabular block text-caption text-ink-muted">
                            {t("providerRef", { ref: l.providerReference })}
                          </span>
                        ) : null}
                      </span>
                    }
                  />
                </li>
              ))}
            </ol>
          </Card>
          <Card elevation="rest" padding="md">
            <CardTitle className="mb-4">{t("travellers")}</CardTitle>
            <ul className="divide-y divide-line">
              {booking.passengers.map((p) => (
                <li key={p.id} className="flex items-center justify-between gap-4 py-2 text-body">
                  <span className="text-ink">{p.fullName}</span>
                  <span className="text-small text-ink-muted">{tType(p.passengerType)}</span>
                </li>
              ))}
            </ul>
          </Card>
        </div>
        <aside className="space-y-6">
          <Card elevation="rest" padding="md">
            <CardTitle className="mb-4">{t("price")}</CardTitle>
            <PriceBreakdown lines={booking.priceBreakdown} total={booking.total} />
          </Card>
          {payment ? (
            <Card elevation="rest" padding="md">
              <CardTitle className="mb-3">{t("payment")}</CardTitle>
              <p className="text-body font-medium text-ink">{tPay(payment.status)}</p>
              {payment.refunded ? (
                <p className="tabular mt-1 text-small text-ink-muted">
                  {t("refunded", { amount: formatMoney(payment.refunded) })}
                </p>
              ) : null}
              {payment.failureCode ? (
                <p className="mt-1 text-small text-ink-muted">
                  {t("failureReason", {
                    reason: tFail(failureKey(payment.failureCode) as "UNKNOWN"),
                  })}
                </p>
              ) : null}
            </Card>
          ) : null}
        </aside>
      </div>

      <CancelDialog booking={booking} open={cancelOpen} onOpenChange={setCancelOpen} />
    </div>
  );
}

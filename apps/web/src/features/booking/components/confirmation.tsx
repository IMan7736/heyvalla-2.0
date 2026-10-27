"use client";

import {
  BookingConfirmed,
  Button,
  Card,
  CardTitle,
  ErrorState,
  Icon,
  Skeleton,
} from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { BookingApiError, bookingApi } from "../lib/api";
import { useBooking } from "../lib/hooks";
import { shortRef } from "../lib/status";
import { BookingProviders } from "./booking-providers";
import { BookingSteps } from "./book-start";
import { LegLine } from "./leg-line";
import { PriceBreakdown } from "./price-breakdown";

export function Confirmation({ bookingId }: { bookingId: string }) {
  return (
    <BookingProviders>
      <ConfirmationInner bookingId={bookingId} />
    </BookingProviders>
  );
}

function ConfirmationInner({ bookingId }: { bookingId: string }) {
  const t = useTranslations("booking.done");
  const tDetail = useTranslations("booking.detail");
  const booking = useBooking(bookingId);

  return (
    <div className="mx-auto max-w-[920px] px-8 pb-24 pt-8">
      <div className="mb-6">
        <BookingSteps current="tickets" />
      </div>
      {booking.isPending ? (
        <div role="status" aria-busy="true" aria-label={tDetail("loading")} className="space-y-4">
          <Skeleton className="mx-auto h-40 w-40 rounded-full" />
          <Skeleton className="mx-auto h-8 w-72" />
          <Skeleton className="h-48 w-full" />
        </div>
      ) : booking.isError ? (
        booking.error instanceof BookingApiError && booking.error.status === 404 ? (
          <ErrorState
            title={tDetail("notFound.title")}
            description={tDetail("notFound.description")}
            action={
              <Button asChild>
                <Link href="/trips">{tDetail("notFound.action")}</Link>
              </Button>
            }
          />
        ) : (
          <ErrorState
            title={tDetail("error.title")}
            description={tDetail("error.description")}
            action={
              <Button onClick={() => void booking.refetch()}>{tDetail("error.retry")}</Button>
            }
          />
        )
      ) : booking.data.status !== "confirmed" ? (
        <ErrorState
          title={t("notConfirmed.title")}
          description={t("notConfirmed.description")}
          action={
            <Button asChild>
              <Link href={`/book/pay/${encodeURIComponent(bookingId)}`}>
                {t("notConfirmed.action")}
              </Link>
            </Button>
          }
          secondaryAction={
            <Button variant="secondary" asChild>
              <Link href={`/trips/${encodeURIComponent(bookingId)}`}>{tDetail("back")}</Link>
            </Button>
          }
        />
      ) : (
        <>
          <BookingConfirmed
            title={t("title")}
            description={t("description")}
            reference={shortRef(booking.data.id)}
            referenceLabel={t("reference")}
          >
            <p className="text-small text-ink-muted">{t("emailNote")}</p>
            <div className="mt-2 flex flex-wrap items-center justify-center gap-3">
              <Button size="lg" asChild>
                <Link href={`/trips/${encodeURIComponent(bookingId)}/tickets`}>
                  <Icon name="ticket" size={18} /> {t("tickets")}
                </Link>
              </Button>
              <Button size="lg" variant="secondary" asChild>
                <a href={bookingApi.pdfUrl(bookingId)} download>
                  <Icon name="download" size={18} /> {t("pdf")}
                </a>
              </Button>
              <Button size="lg" variant="ghost" asChild>
                <Link href="/trips">{t("trips")}</Link>
              </Button>
            </div>
          </BookingConfirmed>
          <Card elevation="rest" padding="md" className="mt-6">
            <CardTitle className="mb-4">{tDetail("legs")}</CardTitle>
            <ol className="mb-5 space-y-4">
              {booking.data.legs.map((l) => (
                <li key={l.id}>
                  <LegLine leg={l.leg} number={l.legIndex + 1} trailing={<span />} />
                </li>
              ))}
            </ol>
            <PriceBreakdown lines={booking.data.priceBreakdown} total={booking.data.total} />
            {booking.data.legs.some((l) => !l.leg.bookable) ? (
              <div className="mt-4 rounded-md bg-sand-200 p-3 text-small text-ink">
                <p className="font-medium">{t("payOnBoardTitle")}</p>
                <p className="text-ink-muted">
                  {t("payOnBoardBody", {
                    legs: booking.data.legs
                      .filter((l) => !l.leg.bookable)
                      .map((l) => `${l.leg.from.name.en} → ${l.leg.to.name.en}`)
                      .join(", "),
                  })}
                </p>
              </div>
            ) : null}
          </Card>
        </>
      )}
    </div>
  );
}

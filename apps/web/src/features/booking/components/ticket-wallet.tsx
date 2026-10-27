"use client";

import {
  Button,
  EmptyState,
  ErrorState,
  Icon,
  Skeleton,
  TicketCard,
  formatDay,
  formatTime,
} from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { BookingApiError, bookingApi } from "../lib/api";
import { useBooking, useTickets } from "../lib/hooks";
import { routeSummary } from "../lib/status";
import type { Booking, Ticket } from "../lib/types";
import { BookingProviders } from "./booking-providers";
import { DisruptionBanners } from "./disruption-banners";

const TICKET_STATES = new Set(["confirmed", "partially_cancelled", "cancelled", "refunded"]);

export function TicketWallet({ bookingId }: { bookingId: string }) {
  return (
    <BookingProviders>
      <WalletInner bookingId={bookingId} />
    </BookingProviders>
  );
}

function WalletInner({ bookingId }: { bookingId: string }) {
  const t = useTranslations("booking.wallet");
  const booking = useBooking(bookingId);
  const hasTickets = booking.data ? TICKET_STATES.has(booking.data.status) : false;
  const tickets = useTickets(bookingId, hasTickets);
  const back = `/trips/${encodeURIComponent(bookingId)}`;

  let body: React.ReactNode;
  if (booking.isPending || (hasTickets && tickets.isPending)) {
    body = (
      <div role="status" aria-busy="true" aria-label={t("loading")} className="flex gap-6">
        <Skeleton className="h-[34rem] w-80 rounded-xl" />
        <Skeleton className="h-[34rem] w-80 rounded-xl" />
      </div>
    );
  } else if (booking.isError || tickets.isError) {
    const notFound = booking.error instanceof BookingApiError && booking.error.status === 404;
    body = (
      <ErrorState
        title={notFound ? t("empty.title") : t("error.title")}
        description={notFound ? t("empty.description") : t("error.description")}
        action={
          notFound ? (
            <Button asChild>
              <Link href="/trips">{t("empty.action")}</Link>
            </Button>
          ) : (
            <Button
              onClick={() => {
                void booking.refetch();
                void tickets.refetch();
              }}
            >
              {t("error.retry")}
            </Button>
          )
        }
      />
    );
  } else if (!hasTickets || (tickets.data?.items.length ?? 0) === 0) {
    body = (
      <EmptyState
        title={t("empty.title")}
        description={t("empty.description")}
        action={
          <Button asChild>
            <Link href={back}>{t("empty.action")}</Link>
          </Button>
        }
      />
    );
  } else {
    body = <Wallet booking={booking.data} tickets={tickets.data?.items ?? []} />;
  }

  return (
    <div className="mx-auto max-w-[1184px] px-8 pb-24 pt-8">
      <header className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <Button variant="ghost" size="sm" asChild className="mb-2">
            <Link href={back}>
              <Icon name="arrow-left" size={16} /> {t("back")}
            </Link>
          </Button>
          <h1 className="font-display text-display text-ink">{t("title")}</h1>
          {booking.data && tickets.data ? (
            <p className="mt-1 text-body text-ink-muted">
              {t("summary", { route: routeText(booking.data), count: tickets.data.items.length })}
            </p>
          ) : null}
        </div>
        {hasTickets ? (
          <Button variant="secondary" asChild>
            <a href={bookingApi.pdfUrl(bookingId)} download>
              <Icon name="download" size={18} /> {t("pdf")}
            </a>
          </Button>
        ) : null}
      </header>
      {booking.data ? (
        <div className="mb-6">
          <DisruptionBanners booking={booking.data} compact />
        </div>
      ) : null}
      {body}
    </div>
  );
}

function routeText(booking: Booking): string {
  const route = routeSummary(booking.legs);
  return route ? `${route.from} → ${route.to}` : "";
}

function Wallet({ booking, tickets }: { booking: Booking; tickets: Ticket[] }) {
  const t = useTranslations("booking.wallet");
  const tModes = useTranslations("planner.modes");
  const tStatus = useTranslations("booking.wallet.ticketStatus");
  const byLeg = booking.legs
    .map((l) => ({ leg: l, tickets: tickets.filter((x) => x.bookingLegId === l.id) }))
    .filter((g) => g.tickets.length > 0);
  const name = (id: string) => booking.passengers.find((p) => p.id === id)?.fullName ?? "";

  return (
    <div className="space-y-10">
      <p className="text-small text-ink-muted">{t("scanHint")}</p>
      {byLeg.map(({ leg, tickets: legTickets }) => (
        <section key={leg.id} aria-labelledby={`leg-${leg.id}`}>
          <h2 id={`leg-${leg.id}`} className="mb-4 font-display text-title text-ink">
            {t("legHeading", { number: leg.legIndex + 1 })}
          </h2>
          <ul className="flex flex-wrap gap-6">
            {legTickets.map((ticket) => (
              <li key={ticket.id}>
                <TicketCard
                  mode={leg.leg.mode}
                  modeLabel={tModes(leg.leg.mode)}
                  fromLabel={leg.leg.from.name.en}
                  toLabel={leg.leg.to.name.en}
                  departTime={formatTime(leg.leg.departAt)}
                  arriveTime={formatTime(leg.leg.arriveAt)}
                  day={formatDay(leg.leg.departAt)}
                  operatorLabel={tModes(leg.leg.mode)}
                  details={[
                    { id: "pax", label: t("passenger"), value: name(ticket.passengerId) },
                    {
                      id: "no",
                      label: t("ticketNumber"),
                      value: ticket.id.slice(-8).toUpperCase(),
                    },
                  ]}
                  status={{
                    label: tStatus(ticket.status),
                    tone:
                      ticket.status === "valid"
                        ? "success"
                        : ticket.status === "used"
                          ? "neutral"
                          : "danger",
                  }}
                  qr={
                    <TicketQr
                      ticket={ticket}
                      alt={t("qrAlt", {
                        from: leg.leg.from.name.en,
                        to: leg.leg.to.name.en,
                        name: name(ticket.passengerId),
                      })}
                      dimmed={ticket.status !== "valid"}
                      errorLabel={t("qrLoadError")}
                    />
                  }
                  qrCaption={ticket.status === "valid" ? t("qrCaption") : tStatus(ticket.status)}
                />
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

function TicketQr({
  ticket,
  alt,
  dimmed,
  errorLabel,
}: {
  ticket: Ticket;
  alt: string;
  dimmed: boolean;
  errorLabel: string;
}) {
  const [failed, setFailed] = useState(false);
  if (failed) {
    return (
      <span
        role="img"
        aria-label={errorLabel}
        className="grid place-items-center text-caption text-ink-muted"
      >
        {errorLabel}
      </span>
    );
  }
  // eslint-disable-next-line @next/next/no-img-element -- the QR is an authenticated SVG from our own BFF; next/image would strip the cookie-bound request.
  return (
    <img
      src={bookingApi.qrUrl(ticket.id)}
      alt={alt}
      width={144}
      height={144}
      className={dimmed ? "opacity-30" : ""}
      onError={() => setFailed(true)}
    />
  );
}

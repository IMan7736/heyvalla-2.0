"use client";

import {
  Button,
  Card,
  EmptyState,
  ErrorState,
  Icon,
  SegmentedControl,
  Skeleton,
  formatDay,
  formatMoney,
  formatTime,
} from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useMemo, useState } from "react";
import { useBookingList } from "../lib/hooks";
import {
  firstDeparture,
  isUpcoming,
  openDisruptions,
  routeSummary,
  shortRef,
  sortTrips,
} from "../lib/status";
import type { Booking } from "../lib/types";
import { BookingProviders } from "./booking-providers";
import { DisruptionBanners } from "./disruption-banners";
import { StatusChip } from "./status-chip";

export function TripsList() {
  return (
    <BookingProviders>
      <TripsListInner />
    </BookingProviders>
  );
}

function TripsListInner() {
  const t = useTranslations("booking.trips");
  const [tab, setTab] = useState<"upcoming" | "past">("upcoming");
  const query = useBookingList();
  const [now] = useState(() => Date.now());
  const bookings = useMemo(() => query.data?.pages.flatMap((p) => p.items) ?? [], [query.data]);
  const shown = useMemo(() => {
    const wantUpcoming = tab === "upcoming";
    return sortTrips(
      bookings.filter((b) => isUpcoming(b, now) === wantUpcoming),
      wantUpcoming,
    );
  }, [bookings, tab, now]);

  return (
    <div className="mx-auto max-w-[920px] px-8 pb-24 pt-10">
      <header className="mb-8 flex items-end justify-between gap-6">
        <div>
          <h1 className="font-display text-display text-ink">{t("title")}</h1>
          <p className="mt-1 text-body text-ink-muted">{t("subtitle")}</p>
        </div>
        <SegmentedControl
          label={t("tabs.label")}
          value={tab}
          onValueChange={(v) => setTab(v === "past" ? "past" : "upcoming")}
          options={[
            { value: "upcoming", label: t("tabs.upcoming") },
            { value: "past", label: t("tabs.past") },
          ]}
        />
      </header>

      {query.isPending ? (
        <div role="status" aria-busy="true" aria-label={t("loading")} className="space-y-4">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-32 w-full rounded-lg" />
          ))}
        </div>
      ) : query.isError ? (
        <ErrorState
          title={t("error.title")}
          description={t("error.description")}
          action={<Button onClick={() => void query.refetch()}>{t("error.retry")}</Button>}
        />
      ) : shown.length === 0 && !query.hasNextPage ? (
        <EmptyState
          title={tab === "upcoming" ? t("empty.upcomingTitle") : t("empty.pastTitle")}
          description={
            tab === "upcoming" ? t("empty.upcomingDescription") : t("empty.pastDescription")
          }
          {...(tab === "upcoming"
            ? {
                action: (
                  <Button asChild>
                    <Link href="/plan">{t("empty.action")}</Link>
                  </Button>
                ),
              }
            : {})}
        />
      ) : (
        <>
          <ul className="space-y-4">
            {shown.map((b) => (
              <li key={b.id}>
                <TripCard booking={b} />
              </li>
            ))}
          </ul>
          {query.hasNextPage ? (
            <div className="mt-6 flex justify-center">
              <Button
                variant="secondary"
                loading={query.isFetchingNextPage}
                onClick={() => void query.fetchNextPage()}
              >
                {query.isFetchingNextPage ? t("loadingMore") : t("loadMore")}
              </Button>
            </div>
          ) : null}
        </>
      )}
    </div>
  );
}

function TripCard({ booking }: { booking: Booking }) {
  const t = useTranslations("booking.trips");
  const route = routeSummary(booking.legs);
  const departure = firstDeparture(booking);
  const routeLabel = route ? `${route.from} → ${route.to}` : t("unknownRoute");
  const disruptions = openDisruptions(booking).length;
  return (
    <Card elevation="rest" padding="none" className="overflow-hidden">
      <Link
        href={
          booking.status === "held"
            ? `/book/pay/${encodeURIComponent(booking.id)}`
            : `/trips/${encodeURIComponent(booking.id)}`
        }
        aria-label={t("card.open", { route: routeLabel })}
        className="block p-5 transition hover:bg-lagoon-50 focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-action"
      >
        <div className="flex items-start justify-between gap-4">
          <div className="min-w-0">
            <h2 className="truncate font-display text-title text-ink">{routeLabel}</h2>
            {departure ? (
              <p className="tabular mt-1 text-body text-ink">
                {formatDay(departure)} · {formatTime(departure)}
              </p>
            ) : null}
            <p className="mt-1 flex flex-wrap gap-x-3 text-caption text-ink-muted">
              <span>{t("card.legs", { count: booking.legs.length })}</span>
              <span>{t("card.passengers", { count: booking.passengers.length })}</span>
              <span className="tabular">{t("card.reference", { id: shortRef(booking.id) })}</span>
            </p>
          </div>
          <div className="flex shrink-0 flex-col items-end gap-2">
            <StatusChip status={booking.status} />
            <p className="tabular text-lead font-semibold text-ink">{formatMoney(booking.total)}</p>
          </div>
        </div>
        {booking.status === "held" && booking.holdExpiresAt ? (
          <p className="mt-3 flex items-center gap-2 text-small text-caution">
            <Icon name="clock" size={16} />{" "}
            {t("card.holdLeft", { time: formatTime(booking.holdExpiresAt) })}
          </p>
        ) : null}
        {disruptions > 0 ? (
          <span className="sr-only">{t("card.disruptions", { count: disruptions })}</span>
        ) : null}
      </Link>
      {disruptions > 0 ? (
        <div className="px-5 pb-5">
          <DisruptionBanners booking={booking} compact />
        </div>
      ) : null}
    </Card>
  );
}

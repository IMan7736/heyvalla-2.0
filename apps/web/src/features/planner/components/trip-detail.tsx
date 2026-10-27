"use client";

import {
  BufferMeter,
  Button,
  Card,
  CardTitle,
  ErrorState,
  JourneyRibbon,
  ModeChip,
  RiskGauge,
  Skeleton,
} from "@dhathuru/ui";
import { Reveal } from "@dhathuru/motion";
import { TripMap, useMapFocus } from "@/features/map";
import { NAV_BACK, NAV_FORWARD } from "@/motion";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { Fragment, Suspense, type ReactNode } from "react";
import { PlannerApiError, fetchLegGeometries, fetchOption } from "../lib/api";
import { formatDate, formatMoney, formatTime } from "../lib/format";
import { bufferTone } from "../lib/results";
import { bookHref, type TravelGroup } from "../lib/search-state";
import type { Leg, TripOption } from "../lib/types";
import { ExplanationList, WarningList } from "./explanation-list";
import { LabelChips } from "./option-card";
import { PlannerProviders } from "./planner-providers";
import { usePresenters } from "./use-presenters";

const KNOWN_FARES = new Set([
  "FERRY_FARE",
  "SPEEDBOAT_FARE",
  "SEAPLANE_FARE",
  "FLIGHT_FARE",
  "BUS_FARE",
  "TAXI_FARE",
  "RESORT_TRANSFER_FARE",
  "SERVICE_FEE",
]);

function Panel({ title, children, id }: { title: string; children: ReactNode; id: string }) {
  return (
    <Card elevation="rest" padding="md" aria-labelledby={id} role="region">
      <CardTitle id={id} className="mb-4">
        {title}
      </CardTitle>
      {children}
    </Card>
  );
}

function LegRow({
  leg,
  index,
  active,
  onHover,
  onToggleFocus,
}: {
  leg: Leg;
  index: number;
  active: boolean;
  onHover: (id: string | null) => void;
  onToggleFocus: (id: string) => void;
}) {
  const t = useTranslations("planner");
  const present = usePresenters();
  return (
    <li
      onMouseEnter={() => onHover(leg.id)}
      onMouseLeave={() => onHover(null)}
      onFocus={() => onHover(leg.id)}
      onBlur={() => onHover(null)}
    >
      <button
        type="button"
        aria-pressed={active}
        aria-label={t("detail.highlightLeg", {
          leg: `${t(`modes.${leg.mode}`)}: ${leg.from.name.en} → ${leg.to.name.en}`,
        })}
        onClick={() => onToggleFocus(leg.id)}
        className={`w-full rounded-lg p-5 text-start shadow-[var(--shadow-hairline)] transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action ${
          active
            ? "bg-lagoon-50 shadow-[inset_0_0_0_2px_var(--color-action)]"
            : "bg-surface hover:shadow-[var(--shadow-rest)]"
        }`}
      >
        <div className="flex items-start gap-4">
          <ModeChip mode={leg.mode} size={44} />
          <div className="min-w-0 flex-1">
            <div className="flex items-baseline justify-between gap-4">
              <h3 className="text-lead font-semibold text-ink">
                <span className="sr-only">{index + 1}. </span>
                {t(`modes.${leg.mode}`)}
              </h3>
              <span className="tabular text-lead font-semibold text-ink">
                {formatMoney(leg.price)}
              </span>
            </div>
            <p className="mt-1 text-body text-ink">
              <span className="tabular font-medium">{formatTime(leg.departAt)}</span>{" "}
              {leg.from.name.en}
              <span aria-hidden="true"> → </span>
              <span className="sr-only"> {t("detail.legTo")} </span>
              <span className="tabular font-medium">{formatTime(leg.arriveAt)}</span>{" "}
              {leg.to.name.en}
            </p>
            <p className="mt-1 flex flex-wrap gap-x-3 text-caption text-ink-muted">
              <span>{present.duration(leg.durationMin)}</span>
              <span>
                {leg.pricing === "estimate" ? t("detail.fareEstimate") : t("detail.fareFixed")}
              </span>
              <span>{leg.bookable ? t("detail.bookable") : t("detail.notBookable")}</span>
            </p>
          </div>
        </div>
      </button>
    </li>
  );
}

function ConnectionRow({ buffer }: { buffer: TripOption["buffers"][number] }) {
  const t = useTranslations("planner.detail");
  const present = usePresenters();
  const tone = bufferTone(buffer);
  return (
    <li
      aria-label={t("connection", { minutes: buffer.minutes })}
      className="flex items-stretch gap-4 ps-[1.35rem]"
    >
      <span
        aria-hidden="true"
        className={`my-1 w-0.5 shrink-0 rounded-full ${tone === "tight" ? "bg-caution" : "bg-lagoon-300"}`}
      />
      <div className="w-full max-w-sm py-2">
        <BufferMeter {...present.bufferMeter(buffer)} />
        <p className="mt-1 text-caption text-ink-muted">
          {t("connectionRequired", { required: buffer.requiredMinutes })} ·{" "}
          {t("connectionComfort", { comfort: buffer.comfortMinutes })}
        </p>
      </div>
    </li>
  );
}

function DelayPanel({ option }: { option: TripOption }) {
  const t = useTranslations("planner.detail");
  const tModes = useTranslations("planner.modes");
  if (option.buffers.length === 0)
    return <p className="text-body text-ink-muted">{t("noConnections")}</p>;
  return (
    <>
      <p className="mb-3 text-small text-ink-muted">{t("ifDelayedIntro")}</p>
      <ul className="space-y-2">
        {option.buffers.map((b) => {
          const leg = option.legs[b.afterLegIndex];
          const spare = Math.max(0, b.minutes - b.requiredMinutes);
          const name = leg ? tModes(leg.mode) : "";
          return (
            <li
              key={b.afterLegIndex}
              className="rounded-sm bg-canvas px-4 py-3 text-small text-ink"
            >
              {spare > 0
                ? t("absorbs", { leg: name, minutes: spare })
                : t("absorbsTight", { leg: name })}
            </li>
          );
        })}
      </ul>
      <p className="mt-3 text-caption text-ink-muted">{t("replanNote")}</p>
    </>
  );
}

function PriceBreakdown({ option }: { option: TripOption }) {
  const t = useTranslations("planner.detail");
  return (
    <>
      <ul className="divide-y divide-line">
        {option.price.breakdown.map((item, i) => (
          <li
            key={`${item.code}-${i}`}
            className="flex items-center justify-between py-2.5 text-body text-ink"
          >
            <span>
              {t(`fareCodes.${KNOWN_FARES.has(item.code) ? item.code : "UNKNOWN"}` as never)}
            </span>
            <span className="tabular">{formatMoney(item.amount)}</span>
          </li>
        ))}
      </ul>
      <div className="mt-3 flex items-center justify-between border-t-2 border-ink/20 pt-3 text-lead font-semibold text-ink">
        <span>{t("total")}</span>
        <span className="tabular">{formatMoney(option.price.total)}</span>
      </div>
    </>
  );
}

/** Map column: real route geometry when the API has it, generated (and flagged) shapes otherwise. */
function RouteMap({
  option,
  focus,
}: {
  option: TripOption;
  focus: ReturnType<typeof useMapFocus>;
}) {
  const ids = option.legs.map((l) => l.geometryRef ?? l.id);
  const geometries = useQuery({
    queryKey: ["planner", "geo", ids],
    queryFn: ({ signal }) => fetchLegGeometries(ids, signal),
    staleTime: 10 * 60_000,
    retry: false,
  });
  return (
    <TripMap
      option={option}
      focus={focus}
      {...(geometries.data ? { geometries: geometries.data } : {})}
      className="h-[26rem] w-full"
    />
  );
}

export function TripDetailView({
  option,
  backHref,
  group = { adults: 1, children: 0, infants: 0 },
}: {
  option: TripOption;
  backHref: string;
  group?: TravelGroup;
}) {
  const t = useTranslations("planner.detail");
  const present = usePresenters();
  const focus = useMapFocus();
  const activeLegId = focus.focusedLegId ?? focus.hoveredLegId;
  const toggleFocus = (id: string) => focus.focusLeg(focus.focusedLegId === id ? null : id);

  return (
    <div className="mx-auto max-w-[1184px] px-8 pb-24 pt-8">
      <Link
        href={backHref}
        transitionTypes={[NAV_BACK]}
        className="inline-flex items-center gap-1.5 rounded-xs text-small font-semibold text-ink underline underline-offset-4 hover:text-link focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
      >
        <span aria-hidden="true">←</span> {t("back")}
      </Link>

      <header className="mt-5">
        <div className="mb-3">
          <LabelChips labels={option.labels} />
        </div>
        <h1 className="tabular font-display text-headline text-ink">
          {formatTime(option.departAt)} → {formatTime(option.arriveAt)}
        </h1>
        <p className="mt-1 text-body text-ink-muted">
          {formatDate(option.departAt)} · {present.duration(option.durationMin)}
        </p>
        <div className="mt-6 max-w-3xl pb-4">
          <JourneyRibbon {...present.ribbon(option)} />
        </div>
      </header>

      <div className="mt-8 grid grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)] gap-8 max-[1360px]:gap-6">
        <div className="space-y-8">
          <section aria-labelledby="timeline-title">
            <h2 id="timeline-title" className="mb-4 font-display text-title text-ink">
              {t("timeline")}
            </h2>
            <ol aria-label={t("legsLabel")} className="space-y-1">
              {option.legs.map((leg, i) => {
                const buffer = option.buffers.find((b) => b.afterLegIndex === i);
                return (
                  <Fragment key={leg.id}>
                    <LegRow
                      leg={leg}
                      index={i}
                      active={activeLegId === leg.id}
                      onHover={focus.hoverLeg}
                      onToggleFocus={toggleFocus}
                    />
                    {buffer ? <ConnectionRow buffer={buffer} /> : null}
                  </Fragment>
                );
              })}
            </ol>
          </section>
          <Panel title={t("ifDelayed")} id="delay-title">
            <DelayPanel option={option} />
          </Panel>
        </div>

        <aside className="flex flex-col gap-6">
          <div
            role="region"
            aria-label={t("mapRegion")}
            className="sticky top-24 overflow-hidden rounded-lg shadow-[var(--shadow-rest)]"
          >
            <RouteMap option={option} focus={focus} />
          </div>
          <Reveal>
            <Panel title={t("why")} id="why-title">
              <div className="mb-4">
                <RiskGauge {...present.risk(option, "bar")} />
              </div>
              <ExplanationList
                explanations={option.explanations.filter(
                  (e) => !option.warnings.some((w) => w.code === e.code),
                )}
              />
            </Panel>
          </Reveal>
          {option.warnings.length > 0 ? (
            <Panel title={t("warnings")} id="warn-title">
              <WarningList warnings={option.warnings} />
            </Panel>
          ) : null}
          <Panel title={t("priceBreakdown")} id="price-title">
            <PriceBreakdown option={option} />
          </Panel>
          <div>
            <Button asChild size="xl" fullWidth>
              <Link
                href={bookHref(option.id, group)}
                transitionTypes={[NAV_FORWARD]}
              >
                {t("book")} · {formatMoney(option.price.total)}
              </Link>
            </Button>
            <p className="mt-2 text-center text-caption text-ink-muted">{t("bookHint")}</p>
          </div>
        </aside>
      </div>
    </div>
  );
}

function DetailBody({ id, backHref, group }: { id: string; backHref: string; group: TravelGroup }) {
  const t = useTranslations("planner.detail");
  const tResults = useTranslations("planner.results");
  const query = useQuery({
    queryKey: ["planner", "option", id],
    queryFn: ({ signal }) => fetchOption(id, signal),
  });

  if (query.isPending) {
    return (
      <div aria-busy="true" role="status" className="mx-auto max-w-[1184px] px-8 py-16">
        <p className="mb-6 font-semibold text-ink">{t("loading")}</p>
        <div className="grid grid-cols-2 gap-8" aria-hidden="true">
          <Skeleton className="h-96 rounded-lg" />
          <Skeleton className="h-96 rounded-lg" />
        </div>
      </div>
    );
  }
  if (query.isError) {
    const gone = query.error instanceof PlannerApiError && query.error.status === 404;
    return (
      <ErrorState
        title={gone ? t("notFoundTitle") : t("errorTitle")}
        description={gone ? t("notFoundBody") : t("errorBody")}
        action={
          gone ? (
            <Button asChild>
              <Link href={backHref}>{t("newSearch")}</Link>
            </Button>
          ) : (
            <Button onClick={() => void query.refetch()}>{tResults("retry")}</Button>
          )
        }
        secondaryAction={
          gone ? undefined : (
            <Button asChild variant="secondary">
              <Link href={backHref}>{t("back")}</Link>
            </Button>
          )
        }
      />
    );
  }
  return <TripDetailView option={query.data} backHref={backHref} group={group} />;
}

export function TripDetail({
  id,
  backHref,
  group,
}: {
  id: string;
  backHref: string;
  group: TravelGroup;
}) {
  return (
    <PlannerProviders>
      <Suspense fallback={null}>
        <DetailBody id={id} backHref={backHref} group={group} />
      </Suspense>
    </PlannerProviders>
  );
}

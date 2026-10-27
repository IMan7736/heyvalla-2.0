"use client";

import {
  Button,
  Checkbox,
  EmptyState,
  ErrorState,
  Field,
  Input,
  Select,
  Skeleton,
} from "@dhathuru/ui";
import { PresenceSwap, Stagger } from "@dhathuru/motion";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useQueryStates } from "nuqs";
import { Suspense, useMemo, useState } from "react";
import { PlannerApiError, fetchPlace, searchTrips } from "../lib/api";
import { formatDate } from "../lib/format";
import { filterOptions, sortOptions } from "../lib/results";
import {
  SORT_KEYS,
  resultsParsers,
  searchKey,
  shiftSearchTime,
  toRequest,
  toSearchParams,
  validateSearch,
  type ResultsState,
} from "../lib/search-state";
import { CompareView } from "./compare-view";
import { OptionCard } from "./option-card";
import { PlannerProviders } from "./planner-providers";
import { SearchFormBody } from "./trip-search-form";

const MAX_COMPARE = 3;
const SHIFT_MINUTES = 120;

function ResultsSkeleton() {
  const t = useTranslations("planner.results");
  return (
    <div aria-busy="true" className="flex flex-col gap-5">
      <p role="status" className="text-small text-ink-muted">
        <span className="font-semibold text-ink">{t("loading")}</span>. {t("loadingHint")}
      </p>
      {[0, 1, 2].map((i) => (
        <div
          key={i}
          aria-hidden="true"
          className="grid grid-cols-[1fr_16rem] overflow-hidden rounded-lg bg-surface shadow-[var(--shadow-rest)]"
        >
          <div className="flex flex-col gap-5 p-6">
            <Skeleton className="h-10 w-1/3" />
            <Skeleton className="h-10 w-full rounded-full" />
            <Skeleton className="h-4 w-1/2" />
          </div>
          <Skeleton className="h-full min-h-40 rounded-none" />
        </div>
      ))}
    </div>
  );
}

function EmptyResults({
  state,
  setState,
}: {
  state: ResultsState;
  setState: (patch: Partial<ResultsState>) => void;
}) {
  const t = useTranslations("planner.results");
  const suggestions: { key: string; apply: () => void }[] = [
    { key: "suggestLater", apply: () => setState(shiftSearchTime(state, SHIFT_MINUTES)) },
    { key: "suggestEarlier", apply: () => setState(shiftSearchTime(state, -SHIFT_MINUTES)) },
    { key: "suggestNextDay", apply: () => setState(shiftSearchTime(state, 1440)) },
  ];
  if (state.risk !== "relaxed")
    suggestions.push({ key: "suggestRelaxed", apply: () => setState({ risk: "relaxed" }) });
  if (state.transfers !== null)
    suggestions.push({ key: "suggestMoreTransfers", apply: () => setState({ transfers: null }) });
  if (state.budget !== null)
    suggestions.push({ key: "suggestAnyBudget", apply: () => setState({ budget: null }) });
  if (state.modes.length > 0)
    suggestions.push({ key: "suggestAllModes", apply: () => setState({ modes: [] }) });
  return (
    <EmptyState
      title={t("noResultsTitle")}
      description={`${t("noResultsBody")} ${t("suggestionsTitle")}:`}
      action={
        <ul className="flex flex-wrap justify-center gap-2">
          {suggestions.map((s) => (
            <li key={s.key}>
              <Button variant="secondary" onClick={s.apply}>
                {t(s.key as never)}
              </Button>
            </li>
          ))}
        </ul>
      }
    />
  );
}

function usePlaceName(id: string | null) {
  const q = useQuery({
    queryKey: ["planner", "place", id],
    queryFn: ({ signal }) => fetchPlace(id as string, signal),
    enabled: id !== null,
    staleTime: 10 * 60_000,
  });
  return q.data?.name.en;
}

function ResultsBody() {
  const t = useTranslations("planner.results");
  const [state, setState] = useQueryStates(resultsParsers, { history: "push" });
  const [compareOpen, setCompareOpen] = useState(false);
  const fromName = usePlaceName(state.from);
  const toName = usePlaceName(state.to);

  const complete = validateSearch(state).length === 0;

  const query = useQuery({
    queryKey: searchKey(state),
    queryFn: ({ signal }) => searchTrips(toRequest(state), signal),
    enabled: complete,
    placeholderData: keepPreviousData,
  });

  const filtered = useMemo(() => {
    const options = query.data?.options ?? [];
    const shown = filterOptions(options, {
      maxPriceMinor: state.maxPrice !== null ? state.maxPrice * 100 : null,
      lowRiskOnly: state.lowRiskOnly === "1",
    });
    return {
      shown: sortOptions(shown, state.sort),
      hidden: options.length - shown.length,
      total: options.length,
    };
  }, [query.data, state.maxPrice, state.lowRiskOnly, state.sort]);

  const carry = toSearchParams(state).toString();
  const compared = (query.data?.options ?? []).filter((o) => state.compare.includes(o.id));
  const dayLabel = state.date ? formatDate(`${state.date}T12:00:00+05:00`) : "";
  const routeLabel = fromName && toName ? `${fromName} → ${toName}` : undefined;

  function toggleCompare(id: string) {
    const next = state.compare.includes(id)
      ? state.compare.filter((c) => c !== id)
      : state.compare.length < MAX_COMPARE
        ? [...state.compare, id]
        : state.compare;
    void setState({ compare: next }, { history: "replace" });
  }

  const showSkeleton =
    complete && (query.isPending || (query.isPlaceholderData && query.isFetching));
  const recommendedId = sortOptions(filtered.shown, "best")[0]?.id;
  const swapKey = `${state.sort}|${state.maxPrice ?? ""}|${state.lowRiskOnly ?? ""}|${carry}`;
  const filtersActive = state.maxPrice !== null || state.lowRiskOnly === "1";

  return (
    <div className="mx-auto max-w-[1184px] px-8 pb-32 pt-10">
      <h1 className="font-display text-headline text-ink">
        {routeLabel
          ? t("heading", { from: fromName ?? "", to: toName ?? "" })
          : t("headingGeneric")}
      </h1>
      {complete ? (
        <p className="tabular mt-1 text-body text-ink-muted">
          {dayLabel} · {state.time}
        </p>
      ) : null}

      <div className="mt-8">
        <SearchFormBody compact />
      </div>

      {!complete ? (
        <div className="mt-8">
          <EmptyState title={t("incompleteTitle")} description={t("incompleteBody")} />
        </div>
      ) : (
        <section className="mt-10" aria-label={t("listLabel")}>
          <div className="mb-5 flex flex-wrap items-center justify-between gap-4">
            <p className="text-lead font-medium text-ink" aria-live="polite">
              {query.isSuccess
                ? t("summary", { count: filtered.shown.length, date: dayLabel })
                : " "}
            </p>
            <div className="flex gap-3">
              <Button
                variant="secondary"
                leadingIcon="arrow-left"
                onClick={() => void setState(shiftSearchTime(state, -SHIFT_MINUTES))}
              >
                {t("earlier")}
              </Button>
              <Button
                variant="secondary"
                trailingIcon="arrow-right"
                onClick={() => void setState(shiftSearchTime(state, SHIFT_MINUTES))}
              >
                {t("later")}
              </Button>
            </div>
          </div>

          <div className="mb-6 flex flex-wrap items-end gap-x-6 gap-y-3 rounded-lg bg-surface px-5 py-4 shadow-[var(--shadow-hairline)]">
            <Field label={t("sortBy")}>
              <Select
                placeholder={t("sortBy")}
                value={state.sort}
                onValueChange={(v) =>
                  void setState({ sort: v as ResultsState["sort"] }, { history: "replace" })
                }
                options={SORT_KEYS.map((k) => ({ value: k, label: t(`sort.${k}`) }))}
                className="w-48"
              />
            </Field>
            <Field label={t("maxPrice")}>
              <Input
                inputMode="numeric"
                value={state.maxPrice ?? ""}
                placeholder={t("maxPricePlaceholder")}
                className="w-36"
                onChange={(e) => {
                  const d = e.target.value.replace(/\D/g, "");
                  void setState({ maxPrice: d === "" ? null : Number(d) }, { history: "replace" });
                }}
              />
            </Field>
            <div className="flex h-10 items-center">
              <Checkbox
                checked={state.lowRiskOnly === "1"}
                onCheckedChange={(c) =>
                  void setState({ lowRiskOnly: c === true ? "1" : null }, { history: "replace" })
                }
                label={t("lowRiskOnly")}
              />
            </div>
            {filtersActive ? (
              <Button
                variant="link"
                onClick={() =>
                  void setState({ maxPrice: null, lowRiskOnly: null }, { history: "replace" })
                }
              >
                {t("clearFilters")}
              </Button>
            ) : null}
          </div>

          {showSkeleton ? (
            <ResultsSkeleton />
          ) : query.isError ? (
            <ErrorState
              title={t("errorTitle")}
              description={
                query.error instanceof PlannerApiError && query.error.status === 422
                  ? t("errorValidation")
                  : query.error instanceof PlannerApiError && query.error.status === 429
                    ? t("errorRateLimited")
                    : t("errorBody")
              }
              action={<Button onClick={() => void query.refetch()}>{t("retry")}</Button>}
            />
          ) : filtered.total === 0 ? (
            <EmptyResults state={state} setState={(patch) => void setState(patch)} />
          ) : (
            <>
              {filtered.hidden > 0 ? (
                <p className="mb-4 text-small text-ink-muted">
                  {t("filteredOut", { count: filtered.hidden })}
                </p>
              ) : null}
              <PresenceSwap swapKey={swapKey}>
                <Stagger className="flex flex-col gap-5">
                  {filtered.shown.map((o) => (
                    <OptionCard
                      key={o.id}
                      option={o}
                      query={carry}
                      {...(routeLabel ? { routeLabel } : {})}
                      recommended={o.id === recommendedId && filtered.shown.length > 1}
                      comparing={state.compare.includes(o.id)}
                      compareDisabled={state.compare.length >= MAX_COMPARE}
                      onToggleCompare={toggleCompare}
                    />
                  ))}
                </Stagger>
              </PresenceSwap>
              <p className="mt-6 text-caption text-ink-muted">{t("syntheticNotice")}</p>
            </>
          )}
        </section>
      )}

      {compared.length > 0 ? (
        <div className="fixed inset-x-0 bottom-0 z-40 bg-surface/95 px-8 py-4 shadow-[var(--shadow-overlay)] backdrop-blur">
          <div className="mx-auto flex max-w-[1184px] items-center justify-between gap-6">
            <p className="text-small font-medium text-ink">
              {t("compareBar", { count: compared.length })}
              {compared.length < 2 ? (
                <span className="text-ink-muted"> · {t("compareHint")}</span>
              ) : null}
              {state.compare.length >= MAX_COMPARE ? (
                <span className="text-ink-muted"> · {t("compareFull")}</span>
              ) : null}
            </p>
            <div className="flex gap-3">
              <Button
                variant="ghost"
                onClick={() => void setState({ compare: [] }, { history: "replace" })}
              >
                {t("compareClear")}
              </Button>
              <Button disabled={compared.length < 2} onClick={() => setCompareOpen(true)}>
                {t("compareOpen")}
              </Button>
            </div>
          </div>
        </div>
      ) : null}
      <CompareView
        options={compared}
        query={carry}
        open={compareOpen}
        onClose={() => setCompareOpen(false)}
      />
    </div>
  );
}

export function ResultsView() {
  return (
    <PlannerProviders>
      <Suspense fallback={null}>
        <ResultsBody />
      </Suspense>
    </PlannerProviders>
  );
}

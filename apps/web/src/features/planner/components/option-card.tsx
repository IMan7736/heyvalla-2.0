"use client";

import { Button, Checkbox, Pill, TripResultCard, type TripLabelTone } from "@dhathuru/ui";
import { NAV_FORWARD, recordRecent } from "@/motion";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { sortExplanations, isKnownCode } from "../lib/explanations";
import { formatDate, formatMoney, formatTime } from "../lib/format";
import type { TripLabel, TripOption } from "../lib/types";
import { useExplain } from "./use-explain";
import { usePresenters } from "./use-presenters";

const LABEL_TONE: Record<TripLabel, TripLabelTone> = {
  best_overall: "lagoon",
  fastest: "neutral",
  cheapest: "coral",
  safest: "success",
};

export function LabelChips({ labels }: { labels: readonly TripLabel[] }) {
  const t = useTranslations("planner.labels");
  if (labels.length === 0) return null;
  return (
    <ul className="flex flex-wrap gap-1.5">
      {labels.map((l) => (
        <li key={l}>
          <Pill tone={LABEL_TONE[l]} dot={LABEL_TONE[l] === "coral"}>
            {t(l)}
          </Pill>
        </li>
      ))}
    </ul>
  );
}

export function tripHref(optionId: string, query: string): string {
  return `/trip/${optionId}${query ? `?${query}` : ""}`;
}

export function OptionCard({
  option,
  query,
  recommended,
  comparing,
  compareDisabled,
  onToggleCompare,
  routeLabel,
}: {
  option: TripOption;
  /** Serialised search params, carried to the detail page so "back" restores the search. */
  query: string;
  recommended: boolean;
  comparing: boolean;
  compareDisabled: boolean;
  onToggleCompare: (id: string) => void;
  /** e.g. "Maafushi to Kooddoo", used to label the trip in the command palette recents. */
  routeLabel?: string;
}) {
  const t = useTranslations("planner.results");
  const tLabels = useTranslations("planner.labels");
  const explain = useExplain();
  const present = usePresenters();
  const hasEstimate = option.legs.some((l) => l.pricing === "estimate");
  const arrive = formatTime(option.arriveAt);
  const depart = formatTime(option.departAt);
  const price = formatMoney(option.price.total);
  const href = tripHref(option.id, query);
  const why = sortExplanations(option.explanations)
    .filter((e) => isKnownCode(e.code))
    .slice(0, 2);
  const labels: { id: string; label: string; tone: TripLabelTone }[] = [
    ...(recommended
      ? [{ id: "recommended", label: t("recommended"), tone: "coral" as const }]
      : []),
    ...option.labels.map((l) => ({ id: l, label: tLabels(l), tone: LABEL_TONE[l] })),
  ];

  return (
    <TripResultCard
      label={t("cardLabel", { arrive, price })}
      departTime={depart}
      arriveTime={arrive}
      arriveDay={formatDate(option.arriveAt)}
      durationLabel={present.duration(option.durationMin)}
      transfersLabel={t("transfers", { count: option.transfers })}
      price={price}
      {...(hasEstimate ? { priceNote: t("priceEstimateNote") } : {})}
      labels={labels}
      ribbon={present.ribbon(option)}
      risk={present.risk(option)}
      explanationsTitle={t("whyTitle")}
      explanations={why.map((e, i) => ({
        id: `${e.code}-${i}`,
        text: explain(e.code, e.params),
        severity: e.severity,
      }))}
      selected={recommended}
      interactive
      action={
        <div className="flex w-full flex-col items-center gap-3">
          <Button asChild fullWidth>
            <Link
              href={href}
              transitionTypes={[NAV_FORWARD]}
              onClick={() =>
                recordRecent({
                  kind: "trip",
                  id: option.id,
                  label: routeLabel ? `${routeLabel} · ${depart}` : `${depart} → ${arrive}`,
                  href,
                })
              }
            >
              {t("view")}
              <span className="sr-only">
                {" "}
                {depart} → {arrive}
              </span>
            </Link>
          </Button>
          <Checkbox
            checked={comparing}
            disabled={compareDisabled && !comparing}
            onCheckedChange={() => onToggleCompare(option.id)}
            label={<span className="whitespace-nowrap">{t("compareAdd")}</span>}
            aria-label={t("compareChoose", { time: `${depart} → ${arrive}` })}
          />
        </div>
      }
    />
  );
}

"use client";

import { BufferMeter, Button, Dialog, JourneyRibbon, RiskGauge } from "@dhathuru/ui";
import { NAV_FORWARD } from "@/motion";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { formatMoney, formatTime } from "../lib/format";
import { weakestBuffer } from "../lib/results";
import type { TripOption } from "../lib/types";
import { LabelChips, tripHref } from "./option-card";
import { usePresenters } from "./use-presenters";

/** Side-by-side comparison of 2 or 3 options in a design-system Dialog (focus trap, Escape, scroll lock). */
export function CompareView({
  options,
  query,
  open,
  onClose,
}: {
  options: TripOption[];
  query: string;
  open: boolean;
  onClose: () => void;
}) {
  const t = useTranslations("planner.results");
  const tUi = useTranslations("ui");
  const present = usePresenters();

  const rows: { key: string; render: (o: TripOption) => React.ReactNode }[] = [
    { key: "departs", render: (o) => <span className="tabular">{formatTime(o.departAt)}</span> },
    { key: "arrives", render: (o) => <span className="tabular">{formatTime(o.arriveAt)}</span> },
    {
      key: "duration",
      render: (o) => <span className="tabular">{present.duration(o.durationMin)}</span>,
    },
    {
      key: "price",
      render: (o) => <span className="tabular font-semibold">{formatMoney(o.price.total)}</span>,
    },
    { key: "transfers", render: (o) => t("transfers", { count: o.transfers }) },
    { key: "risk", render: (o) => <RiskGauge {...present.risk(o, "bar")} /> },
    {
      key: "buffer",
      render: (o) => {
        const w = weakestBuffer(o);
        return w ? <BufferMeter {...present.bufferMeter(w)} /> : "—";
      },
    },
  ];

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => !next && onClose()}
      title={t("compareTitle")}
      closeLabel={tUi("close")}
      size="lg"
    >
      <div className="overflow-x-auto">
        <table className="w-full min-w-[40rem] border-separate border-spacing-y-1 text-start">
          <thead>
            <tr>
              <td />
              {options.map((o) => (
                <th key={o.id} scope="col" className="px-3 pb-3 text-start align-top font-normal">
                  <LabelChips labels={o.labels} />
                  <div className="mt-3">
                    <JourneyRibbon {...present.ribbon(o)} />
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.key} className="odd:bg-canvas">
                <th
                  scope="row"
                  className="rounded-s-sm px-3 py-3 text-start text-small font-semibold text-ink-muted"
                >
                  {t(`compareRow.${row.key}` as never)}
                </th>
                {options.map((o, i) => (
                  <td
                    key={o.id}
                    className={`px-3 py-3 text-body font-medium text-ink ${i === options.length - 1 ? "rounded-e-sm" : ""}`}
                  >
                    {row.render(o)}
                  </td>
                ))}
              </tr>
            ))}
            <tr>
              <td />
              {options.map((o) => (
                <td key={o.id} className="px-3 pt-4">
                  <Button asChild>
                    <Link href={tripHref(o.id, query)} transitionTypes={[NAV_FORWARD]}>
                      {t("view")}
                    </Link>
                  </Button>
                </td>
              ))}
            </tr>
          </tbody>
        </table>
      </div>
    </Dialog>
  );
}

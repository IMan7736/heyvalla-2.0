"use client";

import { useTranslations } from "next-intl";
import { sortExplanations, type Severity } from "../lib/explanations";
import type { Explanation, Warning } from "../lib/types";
import { useExplain } from "./use-explain";

export { useExplain };

const DOT: Record<Severity, { dot: string; icon: string }> = {
  good: { dot: "bg-success", icon: "✓" },
  info: { dot: "bg-action", icon: "i" },
  caution: { dot: "bg-caution", icon: "!" },
};

function Row({ severity, text }: { severity: Severity; text: string }) {
  return (
    <li className="flex items-start gap-3 text-body leading-relaxed text-ink">
      <span
        aria-hidden="true"
        className={`mt-1 grid size-5 shrink-0 place-items-center rounded-full text-[11px] font-bold text-ink-inverse ${DOT[severity].dot}`}
      >
        {DOT[severity].icon}
      </span>
      <span>{text}</span>
    </li>
  );
}

export function ExplanationList({ explanations }: { explanations: readonly Explanation[] }) {
  const explain = useExplain();
  const t = useTranslations("planner.detail");
  const sorted = sortExplanations(explanations);
  if (sorted.length === 0) return <p className="text-body text-ink-muted">{t("whyEmpty")}</p>;
  return (
    <ul className="space-y-3">
      {sorted.map((e, i) => (
        <Row key={`${e.code}-${i}`} severity={e.severity} text={explain(e.code, e.params)} />
      ))}
    </ul>
  );
}

export function WarningList({ warnings }: { warnings: readonly Warning[] }) {
  const explain = useExplain();
  if (warnings.length === 0) return null;
  return (
    <ul className="space-y-3">
      {warnings.map((w, i) => (
        <Row key={`${w.code}-${i}`} severity="caution" text={explain(w.code, w.params)} />
      ))}
    </ul>
  );
}

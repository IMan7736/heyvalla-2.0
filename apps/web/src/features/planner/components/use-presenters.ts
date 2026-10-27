"use client";

import {
  formatDuration,
  type BufferMeterProps,
  type JourneyRibbonProps,
  type RiskGaugeProps,
} from "@dhathuru/ui";
import { useLocale, useTranslations } from "next-intl";
import { formatPercent } from "../lib/format";
import { bufferTone } from "../lib/results";
import type { Buffer, TripOption } from "../lib/types";

/**
 * Maps contract data to the pre-formatted props @dhathuru/ui components expect.
 * The design system has no built-in English: every string here comes from planner.json.
 */
export function usePresenters() {
  const t = useTranslations("planner");
  const locale = useLocale();

  function ribbon(
    option: TripOption,
    variant: "compact" | "detailed" = "compact",
  ): JourneyRibbonProps {
    const last = option.legs[option.legs.length - 1];
    return {
      label: t("journey.label", { count: option.legs.length }),
      variant,
      ...(last ? { destinationLabel: last.to.name.en } : {}),
      legs: option.legs.map((leg) => ({
        id: leg.id,
        mode: leg.mode,
        durationMin: leg.durationMin,
        durationLabel: formatDuration(leg.durationMin, locale),
        fromLabel: leg.from.name.en,
        description: t("journey.legDescription", {
          mode: t(`modes.${leg.mode}`),
          from: leg.from.name.en,
          to: leg.to.name.en,
          duration: formatDuration(leg.durationMin, locale),
        }),
      })),
      buffers: option.buffers.map((b) => {
        const tone = bufferTone(b);
        return {
          afterLegIndex: b.afterLegIndex,
          level: tone,
          label: t("journey.bufferLabel", { minutes: b.minutes }),
          levelLabel: t(`buffer.tone.${tone}`),
        };
      }),
    };
  }

  function risk(option: TripOption, variant: RiskGaugeProps["variant"] = "gauge"): RiskGaugeProps {
    return {
      missProbability: option.risk.missProbability,
      level: option.risk.level,
      label: t("risk.label"),
      levelLabel: t(`risk.${option.risk.level}`),
      // The dial prints valueText inside the arc, so it must be short ("3%"); the bar has room for a sentence.
      valueText:
        variant === "bar"
          ? t("risk.detail", { percent: formatPercent(option.risk.missProbability, locale) })
          : formatPercent(option.risk.missProbability, locale),
      variant,
    };
  }

  function bufferMeter(buffer: Buffer): BufferMeterProps {
    const tone = bufferTone(buffer);
    return {
      minutes: buffer.minutes,
      requiredMinutes: buffer.requiredMinutes,
      comfortMinutes: buffer.comfortMinutes,
      label: t("buffer.label"),
      valueText: t("buffer.value", { minutes: buffer.minutes, tone: t(`buffer.tone.${tone}`) }),
    };
  }

  return {
    ribbon,
    risk,
    bufferMeter,
    duration: (min: number) => formatDuration(min, locale),
    locale,
  };
}

"use client";

import { useTranslations } from "next-intl";
import type { Route, VehicleState } from "../geometry/route";
import type { Timeline } from "../hooks/use-timeline";
import { PauseIcon, PlayIcon } from "./icons";
import { useClock } from "./format";
import styles from "./map.module.css";

interface ScrubberProps {
  route: Route;
  timeline: Timeline;
  vehicle: VehicleState | null;
}

/** Playback bar: play/pause, a leg-coloured track, and a keyboard-operable time slider. */
export function Scrubber({ route, timeline, vehicle }: ScrubberProps) {
  const t = useTranslations("map");
  const clock = useClock();
  const span = Math.max(1, route.endMs - route.startMs);
  const totalMinutes = Math.max(1, Math.round(span / 60_000));
  const elapsedMinutes = Math.round((timeline.timeMs - route.startMs) / 60_000);
  const fraction = Math.min(1, Math.max(0, (timeline.timeMs - route.startMs) / span));

  const leg = vehicle ? route.legs[vehicle.legIndex] : undefined;
  const time = clock(timeline.timeMs);
  let valueText = time;
  if (vehicle && leg) {
    switch (vehicle.phase) {
      case "moving":
        valueText = t("scrubber.moving", {
          time,
          mode: t(`modes.${leg.mode}`),
          place: leg.to.name.en,
        });
        break;
      case "waiting":
        valueText = t("scrubber.waiting", {
          time,
          place: leg.to.name.en,
          minutes: vehicle.waitingMinutes ?? 0,
        });
        break;
      case "before":
        valueText = t("scrubber.before", { time, place: leg.from.name.en });
        break;
      case "after":
        valueText = t("scrubber.after", { time, place: leg.to.name.en });
        break;
    }
  }

  return (
    <div className={styles.scrubber} role="group" aria-label={t("scrubber.group")}>
      <button
        type="button"
        className={styles.playButton}
        onClick={timeline.toggle}
        aria-label={timeline.playing ? t("scrubber.pause") : t("scrubber.play")}
        data-testid="map-play"
      >
        {timeline.playing ? <PauseIcon size={20} /> : <PlayIcon size={20} />}
      </button>
      <div className={styles.sliderWrap}>
        <div className={styles.sliderFocus}>
          <div className={styles.track} aria-hidden="true">
            {route.legs.map((l) => (
              <span
                key={l.id}
                className={styles.segment}
                data-group={l.group}
                style={{
                  insetInlineStart: `${((l.departMs - route.startMs) / span) * 100}%`,
                  inlineSize: `${((l.arriveMs - l.departMs) / span) * 100}%`,
                }}
              />
            ))}
          </div>
          <span
            className={styles.thumb}
            style={{ insetInlineStart: `${fraction * 100}%` }}
            aria-hidden="true"
          />
          <input
            className={styles.range}
            type="range"
            min={0}
            max={totalMinutes}
            step={1}
            value={Math.min(totalMinutes, Math.max(0, elapsedMinutes))}
            aria-label={t("scrubber.label")}
            aria-valuetext={valueText}
            data-testid="map-scrubber"
            onChange={(event) => {
              timeline.pause();
              timeline.seek(route.startMs + Number(event.currentTarget.value) * 60_000);
            }}
          />
        </div>
        <div className={styles.readout} aria-hidden="true">
          <span>{clock(route.startMs)}</span>
          <span className={styles.readoutStatus}>{time}</span>
          <span>{clock(route.endMs)}</span>
        </div>
      </div>
    </div>
  );
}

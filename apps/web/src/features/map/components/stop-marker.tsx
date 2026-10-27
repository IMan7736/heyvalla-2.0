"use client";

import type { Map as MapLibreMap } from "maplibre-gl";
import { useTranslations } from "next-intl";
import { createPortal } from "react-dom";
import type { RouteStop } from "../geometry/route";
import { useMarkerElement } from "../hooks/use-marker-element";
import { useClock } from "./format";
import styles from "./map.module.css";

interface StopMarkerProps {
  map: MapLibreMap;
  stop: RouteStop;
  active?: boolean;
  onActivate?: (stop: RouteStop) => void;
}

/** A numbered, focusable stop on the journey with a hover/focus card. */
export function StopMarker({ map, stop, active = false, onActivate }: StopMarkerProps) {
  const t = useTranslations("map");
  const clock = useClock();
  const { place } = stop;
  const { element } = useMarkerElement(map, [place.geo.lng, place.geo.lat], {
    className: styles.marker,
  });
  const isHub = stop.kind === "interchange" || place.type === "airport";
  const name = place.name.en;
  const times = [
    stop.arriveMs !== undefined ? t("trip.arrives", { time: clock(stop.arriveMs) }) : null,
    stop.departMs !== undefined ? t("trip.departs", { time: clock(stop.departMs) }) : null,
  ].filter(Boolean);
  const label = [t("trip.stopNumber", { number: stop.number }), name, ...times].join(", ");

  return createPortal(
    <>
      <button
        type="button"
        className={styles.stopButton}
        data-hub={isHub}
        data-end={stop.kind === "end"}
        data-active={active}
        data-testid="map-stop"
        aria-label={label}
        onClick={() => onActivate?.(stop)}
      >
        {stop.number}
      </button>
      <div className={styles.card} role="presentation" aria-hidden="true">
        <span className={styles.cardTitle}>{name}</span>
        <span className={styles.cardMeta}>{t(`placeTypes.${place.type}`)}</span>
        {times.map((line) => (
          <span key={line} className={styles.cardMeta}>
            {line}
          </span>
        ))}
        {stop.kind === "interchange" ? (
          <span className={styles.cardTag}>{t("trip.hub")}</span>
        ) : null}
      </div>
    </>,
    element,
  );
}

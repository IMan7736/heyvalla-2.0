"use client";

import type { Map as MapLibreMap } from "maplibre-gl";
import { useTranslations } from "next-intl";
import { useCallback, useState } from "react";
import { usePrefersReducedMotion } from "../hooks/use-reduced-motion";
import type { Place } from "../types";
import { MapCanvas, useMap } from "./map-canvas";
import mapStyles from "./map.module.css";
import { PlaceLayer, type PlaceHover } from "./place-layer";
import { PlaceSearch } from "./place-search";
import styles from "./place-ui.module.css";

export interface MapPickerProps {
  places: readonly Place[];
  /** Selected place id, or null. */
  value: string | null;
  onChange: (place: Place | null) => void;
  /** Visible label for the search box, for example "From". */
  label: string;
  className?: string;
}

function PickerScene({ places, value, onChange, label }: MapPickerProps) {
  const map = useMap() as MapLibreMap;
  const t = useTranslations("map");
  const reduce = usePrefersReducedMotion();
  const [hover, setHover] = useState<PlaceHover | null>(null);
  const selected = places.find((p) => p.id === value) ?? null;

  const pick = useCallback(
    (place: Place | null) => {
      onChange(place);
      if (place)
        map.flyTo({
          center: [place.geo.lng, place.geo.lat],
          zoom: Math.max(map.getZoom(), 10),
          duration: reduce ? 0 : 900,
        });
    },
    [map, onChange, reduce],
  );

  return (
    <>
      <PlaceLayer places={places} selectedId={value} cluster onSelect={pick} onHover={setHover} />
      <div className={styles.toolbar}>
        <PlaceSearch places={places} onSelect={pick} label={label} placeholder={t("picker.hint")} />
        <p className={styles.legend}>{t("picker.hint")}</p>
      </div>
      {hover ? (
        <div
          className={styles.hoverCard}
          style={{ insetInlineStart: hover.x, insetBlockStart: hover.y }}
          aria-hidden="true"
        >
          <strong>{hover.place.name.en}</strong>
          <span>{t(`placeTypes.${hover.place.type}`)}</span>
        </div>
      ) : null}
      <div className={styles.selectedBar} role="status">
        <span>
          {selected ? t("picker.selected", { name: selected.name.en }) : t("picker.none")}
        </span>
        {selected ? (
          <button type="button" className={styles.clear} onClick={() => onChange(null)}>
            {t("picker.clear")}
          </button>
        ) : null}
      </div>
    </>
  );
}

export default function MapPickerImpl(props: MapPickerProps) {
  const t = useTranslations("map");
  const [unavailable, setUnavailable] = useState(false);
  if (unavailable) {
    return (
      <section
        className={mapStyles.fallback}
        aria-label={t("picker.label")}
        data-testid="map-fallback"
      >
        <PlaceSearch places={props.places} onSelect={props.onChange} label={props.label} />
      </section>
    );
  }
  return (
    <div
      className={props.className}
      style={{ position: "relative", blockSize: "100%", inlineSize: "100%" }}
    >
      <MapCanvas label={t("picker.label")} onUnavailable={() => setUnavailable(true)}>
        <PickerScene {...props} />
      </MapCanvas>
    </div>
  );
}

"use client";

import type { Map as MapLibreMap } from "maplibre-gl";
import { useTranslations } from "next-intl";
import { useCallback, useMemo, useState } from "react";
import { MODE_GROUP } from "../geometry/route";
import { boundsOf, padBounds } from "../geometry/geo";
import { usePrefersReducedMotion } from "../hooks/use-reduced-motion";
import { MALDIVES_BOUNDS } from "../config";
import type { NetworkEdge } from "../style/place-layers";
import type { ModeGroup, Place, TransportMode } from "../types";
import { FitIcon } from "./icons";
import { MapCanvas, useMap } from "./map-canvas";
import mapStyles from "./map.module.css";
import { PlaceLayer, type PlaceHover } from "./place-layer";
import { PlaceSearch } from "./place-search";
import styles from "./place-ui.module.css";

export interface NetworkConnection {
  fromId: string;
  toId: string;
  mode: TransportMode;
}

export interface NetworkMapProps {
  places: readonly Place[];
  /** Service counts per place id: busier places glow brighter. */
  serviceCounts?: Readonly<Record<string, number>>;
  /** Scheduled connections between places, drawn as lines and filterable by mode. */
  connections?: readonly NetworkConnection[];
  selectedId?: string | null;
  onSelectPlace?: (place: Place | null) => void;
  className?: string;
}

const GROUPS: ModeGroup[] = ["sea", "air", "road"];
const GROUP_LABEL: Record<ModeGroup, TransportMode> = {
  sea: "ferry",
  air: "domestic_flight",
  road: "bus",
  walk: "walk",
};

function NetworkScene({
  places,
  serviceCounts,
  connections,
  selectedId,
  onSelectPlace,
}: NetworkMapProps) {
  const map = useMap() as MapLibreMap;
  const t = useTranslations("map");
  const reduce = usePrefersReducedMotion();
  const [internalSelected, setInternalSelected] = useState<string | null>(null);
  const [hover, setHover] = useState<PlaceHover | null>(null);
  const [enabled, setEnabled] = useState<ReadonlySet<ModeGroup>>(() => new Set(GROUPS));
  const selected = selectedId !== undefined ? selectedId : internalSelected;
  const selectedPlace = places.find((p) => p.id === selected) ?? null;
  const edges = useMemo<NetworkEdge[]>(
    () =>
      (connections ?? []).map((c) => ({
        fromId: c.fromId,
        toId: c.toId,
        group: MODE_GROUP[c.mode],
      })),
    [connections],
  );
  const hasConnections = edges.length > 0;

  const select = useCallback(
    (place: Place | null) => {
      setInternalSelected(place?.id ?? null);
      onSelectPlace?.(place);
      if (place)
        map.flyTo({
          center: [place.geo.lng, place.geo.lat],
          zoom: Math.max(map.getZoom(), 11),
          duration: reduce ? 0 : 1100,
          essential: false,
        });
    },
    [map, onSelectPlace, reduce],
  );

  const fitAll = () => {
    const b = boundsOf(places.map((p) => [p.geo.lng, p.geo.lat] as [number, number]));
    map.fitBounds(b ? padBounds(b, 0.5) : MALDIVES_BOUNDS, {
      padding: 72,
      duration: reduce ? 0 : 900,
    });
  };

  return (
    <>
      <PlaceLayer
        places={places}
        serviceCounts={serviceCounts}
        selectedId={selected}
        edges={edges}
        enabledGroups={enabled}
        onSelect={select}
        onHover={setHover}
      />
      <div className={styles.toolbar}>
        <PlaceSearch
          places={places}
          onSelect={select}
          label={t("network.search")}
          placeholder={t("network.searchPlaceholder")}
        />
        {hasConnections ? (
          <fieldset className={styles.chips}>
            <legend className={styles.chipLegend}>{t("network.modes")}</legend>
            {GROUPS.map((g) => (
              <label key={g} className={styles.chip}>
                <input
                  type="checkbox"
                  checked={enabled.has(g)}
                  onChange={() =>
                    setEnabled((prev) => {
                      const next = new Set(prev);
                      if (next.has(g)) next.delete(g);
                      else next.add(g);
                      return next;
                    })
                  }
                />
                {t(`modes.${GROUP_LABEL[g]}`)}
              </label>
            ))}
          </fieldset>
        ) : null}
        <button type="button" className={mapStyles.fitButton} onClick={fitAll}>
          <FitIcon /> {t("controls.fitNetwork")}
        </button>
        {serviceCounts ? <p className={styles.legend}>{t("network.density")}</p> : null}
      </div>
      {hover ? (
        <div
          className={styles.hoverCard}
          style={{ insetInlineStart: hover.x, insetBlockStart: hover.y }}
          aria-hidden="true"
        >
          <strong>{hover.place.name.en}</strong>
          <span>{t(`placeTypes.${hover.place.type}`)}</span>
          {serviceCounts?.[hover.place.id] ? (
            <span>{t("network.services", { count: serviceCounts[hover.place.id]! })}</span>
          ) : null}
        </div>
      ) : null}
      {selectedPlace ? (
        <div className={styles.selectedBar} role="status">
          <span>{t("network.selected", { name: selectedPlace.name.en })}</span>
          <button type="button" className={styles.clear} onClick={() => select(null)}>
            {t("network.clear")}
          </button>
        </div>
      ) : null}
    </>
  );
}

export default function NetworkMapImpl(props: NetworkMapProps) {
  const t = useTranslations("map");
  const [unavailable, setUnavailable] = useState(false);
  if (unavailable) {
    return (
      <section
        className={mapStyles.fallback}
        aria-label={t("network.label")}
        data-testid="map-fallback"
      >
        <h3 className={mapStyles.fallbackTitle}>{t("unavailable.title")}</h3>
        <PlaceSearch
          places={props.places}
          onSelect={(p) => props.onSelectPlace?.(p)}
          label={t("network.search")}
        />
      </section>
    );
  }
  return (
    <div
      className={props.className}
      style={{ position: "relative", blockSize: "100%", inlineSize: "100%" }}
    >
      <MapCanvas label={t("network.label")} onUnavailable={() => setUnavailable(true)}>
        <NetworkScene {...props} />
      </MapCanvas>
    </div>
  );
}

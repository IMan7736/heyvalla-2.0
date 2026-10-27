"use client";

import type { GeoJSONSource, MapMouseEvent } from "maplibre-gl";
import { useEffect, useMemo } from "react";
import { placesToFeatures } from "../geometry/places";
import { PLACE_LAYERS, PLACE_SOURCE } from "../style/layer-ids";
import {
  addPlaceLayers,
  removePlaceLayers,
  setNetworkEdges,
  setPlaceData,
  setSelectedPlace,
  type NetworkEdge,
} from "../style/place-layers";
import type { ModeGroup, Place } from "../types";
import { useMap } from "./map-canvas";

export interface PlaceHover {
  place: Place;
  x: number;
  y: number;
}

interface PlaceLayerProps {
  places: readonly Place[];
  serviceCounts?: Readonly<Record<string, number>>;
  selectedId: string | null;
  cluster?: boolean;
  edges?: readonly NetworkEdge[];
  enabledGroups?: ReadonlySet<ModeGroup>;
  onSelect: (place: Place) => void;
  onHover?: (hover: PlaceHover | null) => void;
}

const ALL_GROUPS: ReadonlySet<ModeGroup> = new Set<ModeGroup>(["sea", "air", "road", "walk"]);

/** Renders nothing: wires clustered place points (and optional service edges) into the map. */
export function PlaceLayer({
  places,
  serviceCounts,
  selectedId,
  cluster = true,
  edges,
  enabledGroups = ALL_GROUPS,
  onSelect,
  onHover,
}: PlaceLayerProps) {
  const map = useMap();
  const features = useMemo(() => placesToFeatures(places, serviceCounts), [places, serviceCounts]);
  const byId = useMemo(() => new Map(places.map((p) => [p.id as string, p])), [places]);
  const coords = useMemo(
    () => new Map(places.map((p) => [p.id as string, [p.geo.lng, p.geo.lat] as [number, number]])),
    [places],
  );

  useEffect(() => {
    if (!map) return;
    addPlaceLayers(map, features, cluster);
    return () => removePlaceLayers(map);
    // Layers are created once per map; data changes flow through the effects below.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [map, cluster]);

  useEffect(() => {
    if (map) setPlaceData(map, features);
  }, [map, features]);

  useEffect(() => {
    if (map) setSelectedPlace(map, selectedId);
  }, [map, selectedId]);

  useEffect(() => {
    if (map) setNetworkEdges(map, edges ?? [], coords, enabledGroups);
  }, [map, edges, coords, enabledGroups]);

  useEffect(() => {
    if (!map) return;
    const placeAt = (event: MapMouseEvent) => {
      const hit = map.queryRenderedFeatures(event.point, { layers: [PLACE_LAYERS.points] })[0];
      return hit ? byId.get(String(hit.properties?.id)) : undefined;
    };
    const onClick = async (event: MapMouseEvent) => {
      const clusterHit = map.queryRenderedFeatures(event.point, {
        layers: [PLACE_LAYERS.clusters],
      })[0];
      if (clusterHit && clusterHit.geometry.type === "Point") {
        const zoom = await (map.getSource(PLACE_SOURCE) as GeoJSONSource).getClusterExpansionZoom(
          Number(clusterHit.properties?.cluster_id),
        );
        map.easeTo({
          center: clusterHit.geometry.coordinates as [number, number],
          zoom: zoom + 0.5,
        });
        return;
      }
      const place = placeAt(event);
      if (place) onSelect(place);
    };
    const onMove = (event: MapMouseEvent) => {
      const place = placeAt(event);
      const overCluster =
        map.queryRenderedFeatures(event.point, { layers: [PLACE_LAYERS.clusters] }).length > 0;
      map.getCanvas().style.cursor = place || overCluster ? "pointer" : "";
      onHover?.(place ? { place, x: event.point.x, y: event.point.y } : null);
    };
    map.on("click", onClick);
    map.on("mousemove", onMove);
    return () => {
      map.off("click", onClick);
      map.off("mousemove", onMove);
      map.getCanvas().style.cursor = "";
    };
  }, [map, byId, onSelect, onHover]);

  return null;
}

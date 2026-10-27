"use client";

import { Combobox, Field, type ComboboxOption } from "@dhathuru/ui";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useEffect, useMemo, useState } from "react";
import { fetchPlace, fetchPlaces } from "../lib/api";
import { pushRecent, readRecents } from "../lib/recents";
import type { Place } from "../lib/types";

type PlaceLike = Pick<Place, "id" | "type" | "name" | "geo"> & { code?: string };

function useDebounced<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(id);
  }, [value, delay]);
  return debounced;
}

export function placeLabel(place: PlaceLike): string {
  return place.code ? `${place.name.en} (${place.code})` : place.name.en;
}

export type PlacePickerProps = {
  label: string;
  placeholder: string;
  placeId: string | null;
  onSelect: (place: PlaceLike | null) => void;
  error?: string | undefined;
  inputRef?: React.Ref<HTMLInputElement>;
};

/**
 * Server-driven place search on the design system's ARIA 1.2 Combobox, with recents.
 * Options only ever reflect the settled (debounced) query, so Enter never picks a stale result.
 */
export function PlacePicker({ label, placeholder, placeId, onSelect, error }: PlacePickerProps) {
  const t = useTranslations("planner.form");
  const tType = useTranslations("planner.placeType");
  const tUi = useTranslations("ui");
  const [query, setQuery] = useState("");
  const [recents, setRecents] = useState<PlaceLike[]>(() => readRecents());
  const text = query.trim();
  const debounced = useDebounced(text, 150);
  const searching = text.length >= 2;
  const settled = text === debounced;

  const known = useQuery({
    queryKey: ["planner", "place", placeId],
    queryFn: ({ signal }) => fetchPlace(placeId as string, signal),
    enabled: placeId !== null,
    staleTime: 10 * 60_000,
  });

  const results = useQuery({
    queryKey: ["planner", "places", debounced],
    queryFn: ({ signal }) => fetchPlaces(debounced, signal),
    enabled: debounced.length >= 2,
    staleTime: 5 * 60_000,
  });

  const places: PlaceLike[] = useMemo(() => {
    const list: PlaceLike[] = [...(searching ? (settled ? (results.data ?? []) : []) : recents)];
    // The selected place keeps the id the URL holds, so its label always shows.
    if (known.data && placeId && !list.some((p) => p.id === placeId))
      list.push({ ...known.data, id: placeId });
    return list;
  }, [searching, settled, results.data, recents, known.data, placeId]);
  const options: ComboboxOption[] = useMemo(
    () => places.map((p) => ({ value: p.id, label: placeLabel(p), description: tType(p.type) })),
    [places, tType],
  );

  const loading = searching && (!settled || results.isFetching);

  return (
    <Field label={label} {...(error ? { error } : {})}>
      <Combobox
        size="lg"
        options={options}
        value={placeId}
        placeholder={placeholder}
        emptyLabel={
          results.isError
            ? t("placesError")
            : searching
              ? t("noMatches", { query: text })
              : t("typeToSearch")
        }
        resultsLabel={(count) => tUi("results", { count })}
        clearLabel={tUi("clear")}
        loading={loading}
        loadingLabel={t("loadingPlaces")}
        filter={false}
        onQueryChange={(q) => {
          setQuery(q);
          if (q.trim() === "") setRecents(readRecents());
        }}
        onValueChange={(id) => {
          if (id === null) return onSelect(null);
          const place = places.find((p) => p.id === id);
          if (place) pushRecent(place);
          onSelect(place ?? null);
          setQuery("");
        }}
      />
    </Field>
  );
}

"use client";

import { useTranslations } from "next-intl";
import { useId, useMemo, useState } from "react";
import { searchPlaces } from "../geometry/places";
import type { Place } from "../types";
import styles from "./place-ui.module.css";

interface PlaceSearchProps {
  places: readonly Place[];
  onSelect: (place: Place) => void;
  label: string;
  placeholder?: string;
}

/** Keyboard-first combobox (ARIA 1.2 list autocomplete): the non-pointer way to pick a place. */
export function PlaceSearch({ places, onSelect, label, placeholder }: PlaceSearchProps) {
  const t = useTranslations("map");
  const uid = useId();
  const listId = `${uid}-list`;
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const results = useMemo(() => searchPlaces(places, query), [places, query]);
  const showList = open && query.trim().length > 0;
  const activeIndex = Math.min(active, Math.max(0, results.length - 1));

  const choose = (place: Place) => {
    onSelect(place);
    setQuery(place.name.en);
    setOpen(false);
  };

  return (
    <div className={styles.search}>
      <label className={styles.searchLabel} htmlFor={`${uid}-input`}>
        {label}
      </label>
      <input
        id={`${uid}-input`}
        className={styles.searchInput}
        type="text"
        role="combobox"
        autoComplete="off"
        aria-expanded={showList}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={
          showList && results[activeIndex] ? `${uid}-opt-${activeIndex}` : undefined
        }
        placeholder={placeholder}
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          setActive(0);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => setOpen(false)}
        onKeyDown={(e) => {
          if (e.key === "ArrowDown") {
            e.preventDefault();
            setOpen(true);
            setActive((i) => Math.min(results.length - 1, i + 1));
          } else if (e.key === "ArrowUp") {
            e.preventDefault();
            setActive((i) => Math.max(0, i - 1));
          } else if (e.key === "Enter" && showList && results[activeIndex]) {
            e.preventDefault();
            choose(results[activeIndex]);
          } else if (e.key === "Escape") {
            setOpen(false);
          }
        }}
      />
      <ul
        id={listId}
        role="listbox"
        aria-label={label}
        className={styles.options}
        hidden={!showList}
      >
        {results.map((place, i) => (
          <li
            key={place.id}
            id={`${uid}-opt-${i}`}
            role="option"
            aria-selected={i === activeIndex}
            className={styles.option}
            data-active={i === activeIndex}
            onMouseDown={(e) => {
              e.preventDefault();
              choose(place);
            }}
          >
            <span>{place.name.en}</span>
            <span className={styles.optionType}>{t(`placeTypes.${place.type}`)}</span>
          </li>
        ))}
        {showList && results.length === 0 ? (
          <li className={styles.empty}>{t("network.noResults")}</li>
        ) : null}
      </ul>
      <p className={styles.srOnly} role="status" aria-live="polite">
        {showList ? t("network.resultCount", { count: results.length }) : ""}
      </p>
    </div>
  );
}

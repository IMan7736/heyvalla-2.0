"use client";

import { Command } from "cmdk";
import { motion, spring, usePrefersReducedMotion } from "@dhathuru/motion";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { closePalette, setPaletteView, usePaletteState } from "./palette-store";
import { MIN_QUERY_LENGTH, searchPlaces, type PaletteSuggestion } from "./places-search";
import { readRecents, recordRecent, type Recent } from "./recents";
import { MOTION_ROUTES, placeHref } from "./routes";
import { ShortcutList } from "./shortcut-list";

type SearchResult =
  | { query: string; status: "ready"; items: PaletteSuggestion[] }
  | { query: string; status: "error" };

const DEBOUNCE_MS = 150;

const itemClass =
  "flex cursor-pointer items-center justify-between gap-4 rounded-lg px-3 py-2.5 text-ocean outline-none data-[selected=true]:bg-lagoon data-[selected=true]:text-coconut";
const headingClass =
  "[&_[cmdk-group-heading]]:px-3 [&_[cmdk-group-heading]]:pb-1 [&_[cmdk-group-heading]]:pt-3 [&_[cmdk-group-heading]]:text-xs [&_[cmdk-group-heading]]:font-medium [&_[cmdk-group-heading]]:uppercase [&_[cmdk-group-heading]]:tracking-wide [&_[cmdk-group-heading]]:text-ocean/70";

/**
 * The dialog itself, in its own chunk (cmdk + Radix dialog stay out of every page's initial JS).
 * `warm` renders it once, invisibly and non-modally, on idle so the first real Ctrl+K does not
 * pay for cold code: it touches the same React/Radix/cmdk paths, but nothing is visible, focus is
 * not taken (hidden elements cannot receive it) and the page is not scroll-locked.
 */
export default function PaletteDialog({ warm = false }: { warm?: boolean }) {
  const t = useTranslations("motion");
  const router = useRouter();
  const reduced = usePrefersReducedMotion();
  const { view } = usePaletteState();
  const [query, setQuery] = useState("");
  const [recents] = useState<Recent[]>(() => readRecents());
  const [result, setResult] = useState<SearchResult | null>(null);

  const canSearch = view === "commands" && query.trim().length >= MIN_QUERY_LENGTH;
  const search:
    { status: "idle" | "loading" | "error" } | { status: "ready"; items: PaletteSuggestion[] } =
    !canSearch
      ? { status: "idle" }
      : result?.query !== query
        ? { status: "loading" }
        : result.status === "ready"
          ? { status: "ready", items: result.items }
          : { status: "error" };

  useEffect(() => {
    if (!canSearch) return;
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      searchPlaces(query, controller.signal)
        .then((items) => setResult({ query, status: "ready", items }))
        .catch((error: unknown) => {
          if (controller.signal.aborted) return;
          if (error instanceof DOMException && error.name === "AbortError") return;
          setResult({ query, status: "error" });
        });
    }, DEBOUNCE_MS);
    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [query, canSearch]);

  const go = (href: string, recent?: Omit<Recent, "at" | "href">) => {
    if (recent) recordRecent({ ...recent, href });
    closePalette();
    router.push(href);
  };

  const nav = useMemo(
    () =>
      [
        { id: "home", href: MOTION_ROUTES.home, keys: "g h" },
        { id: "search", href: MOTION_ROUTES.plan, keys: "g s" },
        { id: "trips", href: MOTION_ROUTES.trips, keys: "g t" },
      ] as const,
    [],
  );

  const placeTypeLabel = (type: PaletteSuggestion["type"]) => t(`palette.placeTypes.${type}`);
  const showHelp = view === "help";

  return (
    <Command.Dialog
      open
      modal={!warm}
      onOpenChange={(next) => (next ? undefined : closePalette())}
      label={t(showHelp ? "shortcuts.title" : "palette.title")}
      shouldFilter={!showHelp}
      overlayClassName={`fixed inset-0 z-50 bg-ocean/40${warm ? " invisible" : ""}`}
      contentClassName={`fixed inset-x-0 top-[14vh] z-50 mx-auto w-[min(40rem,calc(100vw-2rem))] overflow-hidden rounded-2xl bg-coconut shadow-2xl ring-1 ring-ocean/10 focus:outline-none${warm ? " invisible pointer-events-none" : ""}`}
    >
      <motion.div
        initial={reduced ? false : { opacity: 0, y: -8, scale: 0.985 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={reduced ? { duration: 0 } : spring.soft}
      >
        {showHelp ? (
          <div>
            <div className="flex items-center justify-between gap-4 border-b border-ocean/10 px-4 py-3">
              <div>
                <h2 className="text-base font-semibold text-ocean">{t("shortcuts.title")}</h2>
                <p className="text-sm text-ocean/70">{t("shortcuts.description")}</p>
              </div>
              <button
                type="button"
                autoFocus
                className="rounded-lg px-3 py-1.5 text-sm font-medium text-lagoon underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-lagoon"
                onClick={() => setPaletteView("commands")}
              >
                {t("shortcuts.back")}
              </button>
            </div>
            <div className="max-h-[60vh] overflow-y-auto px-4 py-3">
              <ShortcutList />
            </div>
          </div>
        ) : (
          <>
            <Command.Input
              value={query}
              onValueChange={setQuery}
              placeholder={t("palette.placeholder")}
              aria-label={t("palette.placeholder")}
              className="w-full border-b border-ocean/10 bg-transparent px-4 py-4 text-base text-ocean placeholder:text-ocean/60 focus:outline-none"
            />
            <Command.List className="max-h-[50vh] overflow-y-auto p-2">
              {search.status === "loading" ? (
                <div role="status" className="px-3 py-3 text-sm text-ocean/70">
                  {t("palette.loading")}
                </div>
              ) : null}
              {search.status === "error" ? (
                <div role="alert" className="px-3 py-3 text-sm text-ocean">
                  {t("palette.error")}
                </div>
              ) : null}
              <Command.Empty className="px-3 py-6 text-center text-sm text-ocean/70">
                {t("palette.empty")}
              </Command.Empty>

              {query.trim() === "" && recents.length > 0 ? (
                <Command.Group heading={t("palette.groups.recent")} className={headingClass}>
                  {recents.map((recent) => (
                    <Command.Item
                      key={`${recent.kind}:${recent.id}`}
                      value={`recent ${recent.label}`}
                      className={itemClass}
                      onSelect={() => go(recent.href, recent)}
                    >
                      <span>{recent.label}</span>
                    </Command.Item>
                  ))}
                </Command.Group>
              ) : null}

              {search.status === "ready" && search.items.length > 0 ? (
                <Command.Group heading={t("palette.groups.places")} className={headingClass}>
                  {search.items.map((place) => (
                    <Command.Item
                      key={place.id}
                      value={`place ${place.id} ${place.name}`}
                      className={itemClass}
                      onSelect={() =>
                        go(placeHref(place.id), { kind: "place", id: place.id, label: place.name })
                      }
                    >
                      <span>{place.name}</span>
                      <span className="text-sm opacity-80">
                        {placeTypeLabel(place.type)}
                        {place.approximate ? ` · ${t("palette.approximate")}` : ""}
                      </span>
                    </Command.Item>
                  ))}
                </Command.Group>
              ) : null}

              <Command.Group heading={t("palette.groups.navigate")} className={headingClass}>
                {nav.map((item) => (
                  <Command.Item
                    key={item.id}
                    value={`go ${t(`palette.nav.${item.id}`)}`}
                    className={itemClass}
                    onSelect={() => go(item.href)}
                  >
                    <span>{t(`palette.nav.${item.id}`)}</span>
                    <kbd className="text-sm opacity-80">{item.keys}</kbd>
                  </Command.Item>
                ))}
              </Command.Group>

              <Command.Group heading={t("palette.groups.actions")} className={headingClass}>
                <Command.Item
                  value={`action ${t("palette.actions.help")}`}
                  className={itemClass}
                  onSelect={() => setPaletteView("help")}
                >
                  <span>{t("palette.actions.help")}</span>
                  <kbd className="text-sm opacity-80">?</kbd>
                </Command.Item>
              </Command.Group>
            </Command.List>
            <div className="flex gap-4 border-t border-ocean/10 px-4 py-2 text-xs text-ocean/70">
              <span>↑↓ {t("palette.footer.navigate")}</span>
              <span>↵ {t("palette.footer.select")}</span>
              <span>Esc {t("palette.footer.close")}</span>
            </div>
          </>
        )}
      </motion.div>
    </Command.Dialog>
  );
}

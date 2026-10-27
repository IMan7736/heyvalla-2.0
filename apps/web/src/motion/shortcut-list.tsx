"use client";

import { useTranslations } from "next-intl";
import { SHORTCUTS, detectMac, type ShortcutDef } from "./shortcuts";

const GROUP_ORDER: ShortcutDef["group"][] = ["global", "navigation", "planner"];

function keyCaps(keys: string, isMac: boolean): string[] {
  return keys.split(" ").map((step) =>
    step
      .split("+")
      .map((part) => (part === "mod" ? (isMac ? "⌘" : "Ctrl") : part.toUpperCase()))
      .join(" "),
  );
}

/** Renders every registered shortcut, grouped. Source of truth is SHORTCUTS. */
export function ShortcutList() {
  const t = useTranslations("motion.shortcuts");
  const isMac = detectMac();
  return (
    <div className="flex flex-col gap-4">
      {GROUP_ORDER.map((group) => {
        const items = SHORTCUTS.filter((s) => s.group === group);
        if (items.length === 0) return null;
        return (
          <section key={group} aria-labelledby={`shortcut-group-${group}`}>
            <h3
              id={`shortcut-group-${group}`}
              className="pb-1 text-xs font-medium uppercase tracking-wide text-ocean/70"
            >
              {t(`groups.${group}`)}
            </h3>
            <dl className="divide-y divide-ocean/10">
              {items.map((shortcut) => (
                <div key={shortcut.id} className="flex items-center justify-between gap-4 py-2">
                  <dt className="text-sm text-ocean">{t(`keys.${shortcut.id}`)}</dt>
                  <dd className="flex items-center gap-1.5 text-sm">
                    {keyCaps(shortcut.keys, isMac).map((cap, index) => (
                      <span key={`${cap}-${index}`} className="flex items-center gap-1.5">
                        {index > 0 ? <span className="text-ocean/70">{t("then")}</span> : null}
                        <kbd className="rounded-md bg-sand px-2 py-0.5 font-mono text-ocean ring-1 ring-ocean/15">
                          {cap}
                        </kbd>
                      </span>
                    ))}
                  </dd>
                </div>
              ))}
            </dl>
          </section>
        );
      })}
    </div>
  );
}

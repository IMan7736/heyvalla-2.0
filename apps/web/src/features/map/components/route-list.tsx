"use client";

import { useTranslations } from "next-intl";
import type { Route } from "../geometry/route";
import { ModeIcon } from "./icons";
import { useClock } from "./format";
import styles from "./map.module.css";

interface RouteListProps {
  route: Route;
  focusedLegId?: string | null;
  onFocusLeg?: (legId: string) => void;
  className?: string;
  /** Visually hidden variant used as the screen-reader alternative beside the map. */
  hidden?: boolean;
}

/** The journey as a list. It is the keyboard / screen-reader alternative to the map. */
export function RouteList({
  route,
  focusedLegId,
  onFocusLeg,
  className,
  hidden = false,
}: RouteListProps) {
  const t = useTranslations("map");
  const clock = useClock();
  return (
    <ol
      className={[styles.stepList, hidden ? styles.srOnly : "", className]
        .filter(Boolean)
        .join(" ")}
      aria-label={t("trip.routeSteps")}
    >
      {route.legs.map((leg) => {
        const title = t("trip.legSummary", {
          mode: t(`modes.${leg.mode}`),
          from: leg.from.name.en,
          to: leg.to.name.en,
          depart: clock(leg.departMs),
          arrive: clock(leg.arriveMs),
        });
        return (
          <li key={leg.id} className={styles.stepItem} data-active={focusedLegId === leg.id}>
            <span className={styles.modeChip} data-group={leg.group}>
              <ModeIcon mode={leg.mode} size={16} />
            </span>
            {onFocusLeg ? (
              <button
                type="button"
                className={styles.stepButton}
                onClick={() => onFocusLeg(leg.id)}
              >
                <span className={styles.stepText}>
                  <span className={styles.stepTitle}>{title}</span>
                  {leg.approximate ? (
                    <span className={styles.stepMeta}>{t("approximate.legBadge")}</span>
                  ) : null}
                </span>
              </button>
            ) : (
              <span className={styles.stepText}>
                <span className={styles.stepTitle}>{title}</span>
                {leg.approximate ? (
                  <span className={styles.stepMeta}>{t("approximate.legBadge")}</span>
                ) : null}
              </span>
            )}
          </li>
        );
      })}
    </ol>
  );
}

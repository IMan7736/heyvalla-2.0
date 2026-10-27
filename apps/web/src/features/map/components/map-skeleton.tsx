"use client";

import { useTranslations } from "next-intl";
import styles from "./map.module.css";

/** Shown while the (lazy) map bundle downloads. Same footprint as the map, so nothing jumps. */
export function MapSkeleton({ className }: { className?: string }) {
  const t = useTranslations("map");
  return (
    <div
      className={[styles.canvasRoot, className].filter(Boolean).join(" ")}
      data-testid="map-skeleton"
    >
      <div className={styles.skeleton} role="status" aria-live="polite">
        <span className={styles.skeletonShimmer} aria-hidden="true" />
        <span className={styles.srOnly}>{t("loading")}</span>
      </div>
    </div>
  );
}

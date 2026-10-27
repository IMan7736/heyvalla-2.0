import { brand } from "../theme/tokens";

/**
 * Map symbol icons drawn as SVG and rasterised at runtime, so no binary sprite sheet has to be
 * built or kept in sync. Ids match the `icon-image` values used in create-style.ts.
 */
const svg = (body: string) =>
  `<svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24">${body}</svg>`;

export const MAP_ICONS: Record<string, string> = {
  airport: svg(
    `<circle cx="12" cy="12" r="10.5" fill="${brand.ocean}" stroke="#fff" stroke-width="1.5"/><path fill="#fff" d="M12 4.6c.6 0 1 .5 1 1.1v3.7l5.7 3.6v1.7l-5.7-1.8v3l1.7 1.3v1.3L12 17.6l-2.7.9v-1.3L11 15.9v-3l-5.7 1.8V13l5.7-3.6V5.7c0-.6.4-1.1 1-1.1z"/>`,
  ),
  jetty: svg(
    `<circle cx="12" cy="12" r="10.5" fill="${brand.lagoon}" stroke="#fff" stroke-width="1.5"/><path fill="none" stroke="#fff" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" d="M12 6.2v10M8.8 8.4h6.4M7 13.5c0 2.6 2.2 4.1 5 4.1s5-1.5 5-4.1"/>`,
  ),
};

/** Registers every icon on the map once; safe to call again after a style reload. */
export async function registerMapIcons(map: {
  hasImage(id: string): boolean;
  addImage(
    id: string,
    image: ImageBitmap | HTMLImageElement,
    options?: { pixelRatio?: number },
  ): void;
}): Promise<void> {
  await Promise.all(
    Object.entries(MAP_ICONS).map(
      ([id, markup]) =>
        new Promise<void>((resolve) => {
          if (map.hasImage(id)) return resolve();
          const image = new Image(48, 48);
          image.onload = () => {
            if (!map.hasImage(id)) map.addImage(id, image, { pixelRatio: 2 });
            resolve();
          };
          image.onerror = () => resolve();
          image.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(markup)}`;
        }),
    ),
  );
}

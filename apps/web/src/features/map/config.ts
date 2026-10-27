/**
 * Where the basemap data lives. In dev, `node infra/tiles/serve.mjs` serves everything on port
 * 8081; in production the same files sit on object storage with HTTP Range support.
 * `NEXT_PUBLIC_TILES_URL` is the base URL (no trailing slash).
 */
export const DEFAULT_TILES_BASE_URL = "http://localhost:8081";

export function tilesBaseUrl(): string {
  const configured = process.env.NEXT_PUBLIC_TILES_URL?.trim();
  return (configured || DEFAULT_TILES_BASE_URL).replace(/\/+$/, "");
}

/** Maldives framing: all atolls from Haa Alif to Addu. */
export const MALDIVES_CENTER: [number, number] = [73.2, 3.2];
export const MALDIVES_BOUNDS: [[number, number], [number, number]] = [
  [72.45, -0.85],
  [73.95, 7.2],
];
export const MIN_ZOOM = 5;
export const MAX_ZOOM = 17;

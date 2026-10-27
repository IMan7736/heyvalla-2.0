/** Routes the global layer navigates to. Owned by the feature agents; change here if they move. */
export const MOTION_ROUTES = {
  home: "/",
  plan: "/plan",
  trips: "/trips",
} as const;

/** Where picking an island in the palette sends the traveller (planner reads `to` via nuqs). */
export function placeHref(placeId: string): string {
  return `${MOTION_ROUTES.plan}?to=${encodeURIComponent(placeId)}`;
}

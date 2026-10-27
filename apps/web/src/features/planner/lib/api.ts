import type {
  LegFeatureCollection,
  Place,
  TripOption,
  TripSearchRequest,
  TripSearchResponse,
} from "./types";

export class PlannerApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
  ) {
    super(code);
    this.name = "PlannerApiError";
  }
}

async function readJson<T>(response: Response): Promise<T> {
  if (response.ok) return (await response.json()) as T;
  const body = (await response.json().catch(() => null)) as { code?: unknown } | null;
  throw new PlannerApiError(
    response.status,
    typeof body?.code === "string" ? body.code : "UNKNOWN",
  );
}

export async function fetchPlaces(query: string, signal?: AbortSignal): Promise<Place[]> {
  const params = new URLSearchParams({ query });
  const data = await readJson<{ items: Place[] }>(
    await fetch(`/plan/api/places?${params}`, { ...(signal ? { signal } : {}) }),
  );
  return data.items;
}

export async function fetchPlace(id: string, signal?: AbortSignal): Promise<Place> {
  const params = new URLSearchParams({ id });
  return readJson<Place>(
    await fetch(`/plan/api/places?${params}`, { ...(signal ? { signal } : {}) }),
  );
}

export async function searchTrips(
  request: TripSearchRequest,
  signal?: AbortSignal,
): Promise<TripSearchResponse> {
  return readJson<TripSearchResponse>(
    await fetch("/plan/api/search", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(request),
      ...(signal ? { signal } : {}),
    }),
  );
}

export async function fetchOption(id: string, signal?: AbortSignal): Promise<TripOption> {
  return readJson<TripOption>(
    await fetch(`/plan/api/options/${encodeURIComponent(id)}`, { ...(signal ? { signal } : {}) }),
  );
}

export async function fetchLegGeometries(
  ids: string[],
  signal?: AbortSignal,
): Promise<LegFeatureCollection> {
  const params = new URLSearchParams({ ids: ids.join(",") });
  return readJson<LegFeatureCollection>(
    await fetch(`/plan/api/geo?${params}`, { ...(signal ? { signal } : {}) }),
  );
}

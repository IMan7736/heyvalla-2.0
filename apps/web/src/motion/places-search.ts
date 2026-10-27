import { z } from "zod";

const placeSchema = z.object({
  id: z.string().min(1),
  type: z.enum(["island", "atoll", "airport", "jetty", "taxi_stand", "poi", "address"]),
  name: z.object({ en: z.string() }).catchall(z.string()),
  approximate: z.boolean().optional(),
});
const responseSchema = z.object({ items: z.array(placeSchema) });

export type PaletteSuggestion = {
  id: string;
  type: z.infer<typeof placeSchema>["type"];
  name: string;
  approximate: boolean;
};

export const MIN_QUERY_LENGTH = 2;

/** Browser code goes through the BFF route, never straight to the .NET API. */
export const PLACES_ENDPOINT = "/api/places";

export async function searchPlaces(
  query: string,
  signal: AbortSignal,
  fetcher: typeof fetch = fetch,
): Promise<PaletteSuggestion[]> {
  const trimmed = query.trim();
  if (trimmed.length < MIN_QUERY_LENGTH) return [];
  const url = `${PLACES_ENDPOINT}?${new URLSearchParams({ query: trimmed, limit: "6" })}`;
  const response = await fetcher(url, { signal, headers: { accept: "application/json" } });
  if (!response.ok) throw new Error(`places search failed: ${response.status}`);
  const parsed = responseSchema.parse(await response.json());
  return parsed.items.map((p) => ({
    id: p.id,
    type: p.type,
    name: p.name.en,
    approximate: p.approximate ?? false,
  }));
}

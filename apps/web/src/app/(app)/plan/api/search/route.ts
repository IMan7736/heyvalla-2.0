import { NextResponse, type NextRequest } from "next/server";
import { z } from "zod";
import { createApiClient } from "@/lib/api-client";

const modeSchema = z.enum([
  "ferry",
  "speedboat",
  "seaplane",
  "domestic_flight",
  "bus",
  "taxi",
  "walk",
  "resort_transfer",
]);

const bodySchema = z.object({
  from: z.string().min(1),
  to: z.string().min(1),
  date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/),
  time: z.string().regex(/^([01]\d|2[0-3]):[0-5]\d$/),
  timeMode: z.enum(["departAfter", "arriveBy"]),
  passengers: z.object({
    adults: z.number().int().min(1).max(9),
    children: z.number().int().min(0).max(9).optional(),
    infants: z.number().int().min(0).max(9).optional(),
  }),
  preferences: z.object({
    riskTolerance: z.enum(["cautious", "balanced", "relaxed"]),
    maxTransfers: z.number().int().min(0).max(8).optional(),
    budgetMax: z
      .object({ amountMinor: z.number().int().min(0), currency: z.literal("MVR") })
      .optional(),
    modes: z.array(modeSchema).optional(),
  }),
});

/** BFF: forwards a trip search to the API. */
export async function POST(request: NextRequest) {
  const parsed = bodySchema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ code: "VALIDATION_FAILED", status: 422 }, { status: 422 });
  }
  try {
    const { data, error, response } = await createApiClient().POST("/trips/search", {
      body: parsed.data,
      signal: request.signal,
    });
    if (data) return NextResponse.json(data);
    return NextResponse.json(error ?? { code: "UPSTREAM_ERROR" }, { status: response.status });
  } catch {
    return NextResponse.json({ code: "UPSTREAM_UNAVAILABLE" }, { status: 502 });
  }
}

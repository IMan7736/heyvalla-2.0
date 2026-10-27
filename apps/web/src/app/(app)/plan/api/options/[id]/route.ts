import { NextResponse, type NextRequest } from "next/server";
import { createApiClient } from "@/lib/api-client";

/** BFF: re-read one option by id (trip detail page). */
export async function GET(_request: NextRequest, ctx: { params: Promise<{ id: string }> }) {
  const { id } = await ctx.params;
  try {
    const { data, error, response } = await createApiClient().GET("/trips/options/{optionId}", {
      params: { path: { optionId: id } },
    });
    if (data) return NextResponse.json(data);
    return NextResponse.json(error ?? { code: "UPSTREAM_ERROR" }, { status: response.status });
  } catch {
    return NextResponse.json({ code: "UPSTREAM_UNAVAILABLE" }, { status: 502 });
  }
}

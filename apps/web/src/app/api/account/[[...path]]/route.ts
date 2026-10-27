import { NextResponse, type NextRequest } from "next/server";
import { isSameOrigin } from "@/lib/session/request";
import { authedCall, clearTokens, relay } from "@/lib/session";

/** Allow-list of /me paths the browser may reach through the BFF. */
const ALLOWED = [
  /^$/,
  /^password$/,
  /^email$/,
  /^preferences$/,
  /^sessions$/,
  /^sessions\/revoke-others$/,
  /^sessions\/ses_[0-9A-HJKMNP-TV-Z]{26}$/,
  /^travellers$/,
  /^travellers\/trv_[0-9A-HJKMNP-TV-Z]{26}$/,
];
const METHODS = new Set(["GET", "POST", "PATCH", "DELETE"]);

async function handle(request: NextRequest, { params }: { params: Promise<{ path?: string[] }> }) {
  const { path } = await params;
  const joined = (path ?? []).join("/");
  if (!METHODS.has(request.method) || !ALLOWED.some((r) => r.test(joined))) {
    return NextResponse.json({ code: "NOT_FOUND" }, { status: 404 });
  }
  if (request.method !== "GET" && !isSameOrigin(request.headers, request.nextUrl.origin)) {
    return NextResponse.json({ code: "FORBIDDEN" }, { status: 403 });
  }
  const body: unknown =
    request.method === "GET" ? undefined : await request.json().catch(() => undefined);
  const result = await authedCall(`/me${joined ? `/${joined}` : ""}`, {
    method: request.method,
    ...(body !== undefined ? { body } : {}),
  });
  if (!result) return NextResponse.json({ code: "AUTH_REQUIRED" }, { status: 401 });
  if (request.method === "DELETE" && joined === "" && result.status === 204) await clearTokens();
  return relay(result);
}

export { handle as GET, handle as POST, handle as PATCH, handle as DELETE };

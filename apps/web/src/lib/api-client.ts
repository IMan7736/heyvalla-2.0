import createClient from "openapi-fetch";
import type { paths } from "@dhathuru/contracts";
import { parseServerEnv, resolveApiBaseUrl } from "@/env";

/**
 * Typed client for server code (route handlers, server components, BFF).
 * Browser code never calls the API directly: it goes through Next route handlers
 * so tokens stay in httpOnly cookies.
 */
export function createApiClient(options: { baseUrl?: string; fetch?: typeof fetch } = {}) {
  return createClient<paths>({
    baseUrl: options.baseUrl ?? resolveApiBaseUrl(parseServerEnv()),
    ...(options.fetch ? { fetch: options.fetch } : {}),
  });
}

export type ApiClient = ReturnType<typeof createApiClient>;

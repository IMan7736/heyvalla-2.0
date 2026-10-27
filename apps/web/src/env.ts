import { z } from "zod";

const serverSchema = z.object({
  /** `mock` talks to the Prism server from `pnpm mock`; `real` talks to the .NET API. */
  API_MODE: z.enum(["mock", "real"]).default("mock"),
  /** Real API base including the `/v1` prefix. */
  API_BASE_URL: z.url().default("http://localhost:5100/v1"),
  /** Prism mock base. Prism serves the spec paths without the `/v1` prefix. */
  API_MOCK_URL: z.url().default("http://localhost:4010"),
});

const publicSchema = z.object({
  /** HTTPS URL of the PMTiles archive (needs HTTP Range support). Empty until COMPASS configures it. */
  NEXT_PUBLIC_TILES_URL: z.union([z.url(), z.literal("")]).default(""),
});

export type ServerEnv = z.infer<typeof serverSchema>;
export type PublicEnv = z.infer<typeof publicSchema>;

export function parseServerEnv(
  source: Record<string, string | undefined> = process.env,
): ServerEnv {
  return serverSchema.parse(source);
}

export function parsePublicEnv(
  source: Record<string, string | undefined> = process.env,
): PublicEnv {
  return publicSchema.parse({ NEXT_PUBLIC_TILES_URL: source.NEXT_PUBLIC_TILES_URL });
}

/** Base URL the server-side API client must use for the current mode. */
export function resolveApiBaseUrl(env: ServerEnv = parseServerEnv()): string {
  return env.API_MODE === "mock" ? env.API_MOCK_URL : env.API_BASE_URL;
}

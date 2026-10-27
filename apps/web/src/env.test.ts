import { describe, expect, it } from "vitest";
import { parseServerEnv, parsePublicEnv, resolveApiBaseUrl } from "./env";

describe("env", () => {
  it("defaults to the mock API", () => {
    const env = parseServerEnv({});
    expect(env.API_MODE).toBe("mock");
    expect(resolveApiBaseUrl(env)).toBe("http://localhost:4010");
  });

  it("switches to the real API with API_MODE=real", () => {
    const env = parseServerEnv({ API_MODE: "real" });
    expect(resolveApiBaseUrl(env)).toBe("http://localhost:5100/v1");
  });

  it("rejects an unknown mode and a malformed URL", () => {
    expect(() => parseServerEnv({ API_MODE: "live" })).toThrow();
    expect(() => parseServerEnv({ API_BASE_URL: "not a url" })).toThrow();
  });

  it("accepts an empty tiles URL", () => {
    expect(parsePublicEnv({}).NEXT_PUBLIC_TILES_URL).toBe("");
    expect(() => parsePublicEnv({ NEXT_PUBLIC_TILES_URL: "nope" })).toThrow();
  });
});

import { describe, expect, it } from "vitest";
import config from "../../next.config";

describe("security headers", () => {
  it("sends a CSP that blocks framing and plugins", async () => {
    const rules = await (
      config as { headers: () => Promise<{ headers: { key: string; value: string }[] }[]> }
    ).headers();
    const csp = rules[0]?.headers.find((h) => h.key === "Content-Security-Policy")?.value ?? "";
    expect(csp).toContain("frame-ancestors 'none'");
    expect(csp).toContain("object-src 'none'");
    expect(csp).toContain("default-src 'self'");
    expect(csp).toContain("worker-src 'self' blob:");
  });
});

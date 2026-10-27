import { defineConfig, devices } from "@playwright/test";
import path from "node:path";

// Run from apps/web (pnpm map:e2e); paths below are relative to it.
const webRoot = process.cwd();
const repoRoot = path.resolve(webRoot, "../..");

/** Map feature smoke tests against the standalone lab (port 3107) and the dev tile server (8081). */
export default defineConfig({
  testDir: ".",
  timeout: 90_000,
  fullyParallel: false,
  reporter: [["list"]],
  use: {
    baseURL: "http://127.0.0.1:3107",
    ...devices["Desktop Chrome"],
    viewport: { width: 1440, height: 900 },
    launchOptions: {
      args: [
        "--use-gl=angle",
        "--use-angle=swiftshader",
        "--enable-unsafe-swiftshader",
        "--ignore-gpu-blocklist",
      ],
    },
  },
  webServer: [
    {
      command: "node infra/tiles/serve.mjs",
      cwd: repoRoot,
      url: "http://localhost:8081/land.geojson",
      reuseExistingServer: true,
    },
    {
      command: "pnpm map:lab",
      cwd: webRoot,
      url: "http://127.0.0.1:3107",
      reuseExistingServer: true,
      timeout: 120_000,
    },
  ],
});

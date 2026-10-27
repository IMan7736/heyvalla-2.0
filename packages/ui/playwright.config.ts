import { defineConfig, devices } from "@playwright/test";

const port = Number(process.env.PORT ?? 3106);

/** Visual baselines and a11y checks for the design-system gallery (/dev/ui) and the landing page. */
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  reporter: [["list"]],
  expect: { toHaveScreenshot: { maxDiffPixelRatio: 0.01, animations: "disabled" } },
  use: { baseURL: `http://localhost:${port}`, trace: "on-first-retry" },
  projects: [
    {
      name: "desktop-1440",
      use: { ...devices["Desktop Chrome"], viewport: { width: 1440, height: 900 } },
    },
    {
      name: "desktop-1280",
      use: { ...devices["Desktop Chrome"], viewport: { width: 1280, height: 800 } },
    },
  ],
  webServer: {
    command: `NEXT_DIST_DIR=.next-lagoon PORT=${port} pnpm --filter web exec next dev --turbopack -p ${port}`,
    url: `http://localhost:${port}/dev/ui`,
    reuseExistingServer: true,
    timeout: 180_000,
  },
});

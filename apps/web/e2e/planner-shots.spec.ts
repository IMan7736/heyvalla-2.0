import { test } from "@playwright/test";

/** Manual visual review helper: SHOTS_DIR=/path pnpm e2e planner-shots. Skipped unless SHOTS_DIR is set. */
const dir = process.env.SHOTS_DIR;
const FROM = "isl_01K8Z3M5Q7R9T2V4W6X8Y0A1B2";
const TO = "isl_01K8Z3M5Q7R9T2V4W6X8Y0A1F2";
const RESULTS = `/results?from=${FROM}&to=${TO}&date=2099-01-01&time=17:00&mode=arriveBy&adults=2`;

test.use({
  launchOptions: {
    args: ["--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"],
  },
});
test.skip(!dir, "set SHOTS_DIR to capture screenshots");
test.setTimeout(180_000);
test.beforeEach(async ({ context }) => {
  await context.addCookies([{ name: "dh_rt", value: "e2e", url: "http://localhost:3108" }]);
});

for (const width of [1440, 1280]) {
  test(`screens at ${width}`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const search = await page.request.post("/plan/api/search", {
      data: {
        from: FROM,
        to: TO,
        date: "2099-01-01",
        time: "17:00",
        timeMode: "arriveBy",
        passengers: { adults: 2 },
        preferences: { riskTolerance: "balanced" },
      },
    });
    const first = ((await search.json()) as { options: unknown[] }).options[0];
    await page.route("**/plan/api/options/*", (route) => route.fulfill({ json: first }));
    for (const [name, url] of [
      ["plan", "/plan"],
      ["results", RESULTS],
      ["trip", `/trip/trp_01K8Z3M5Q7R9T2V4W6X8Y0E1F2?${RESULTS.split("?")[1]}`],
    ] as const) {
      await page.goto(url);
      await page.waitForTimeout(name === "trip" ? 9000 : 3500);
      await page.screenshot({ path: `${dir}/${name}-${width}.png`, fullPage: true });
    }
  });
}

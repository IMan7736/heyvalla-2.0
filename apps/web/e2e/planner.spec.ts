import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

// The shared dev machine is often heavily loaded by parallel agents; keep waits generous.
test.setTimeout(120_000);
const slowExpect = expect.configure({ timeout: 20_000 });

const FROM = "isl_01K8Z3M5Q7R9T2V4W6X8Y0A1B2";
const TO = "isl_01K8Z3M5Q7R9T2V4W6X8Y0A1F2";
const RESULTS = `/results?from=${FROM}&to=${TO}&date=2099-01-01&time=17:00&mode=arriveBy&adults=2`;

async function expectNoA11yViolations(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag22aa"])
    .analyze();
  slowExpect(results.violations.map((v) => `${v.id}: ${v.nodes[0]?.target.join(" ")}`)).toEqual([]);
}

test.describe("planner", () => {
  test.beforeEach(async ({ context, baseURL }) => {
    // Placeholder session so the auth proxy lets /trip/* through (see handoff CURRENT-to-DHONI).
    await context.addCookies([
      { name: "dh_rt", value: "e2e", url: baseURL ?? "http://localhost:3108" },
    ]);
  });

  test("search form validates, then search -> results -> detail -> book hand-off", async ({
    page,
  }) => {
    // The static Prism mock returns the same place for every query; give each query a distinct place.
    const places: Record<string, object> = {
      [FROM]: {
        id: FROM,
        type: "island",
        name: { en: "Maafushi" },
        geo: { lat: 3.94, lng: 73.49 },
        isHub: false,
      },
      [TO]: {
        id: TO,
        type: "island",
        name: { en: "Kooddoo" },
        geo: { lat: 0.73, lng: 73.43 },
        isHub: false,
      },
    };
    await page.route("**/plan/api/places?id=*", (route) =>
      route.fulfill({
        json: places[new URL(route.request().url()).searchParams.get("id") as string],
      }),
    );
    await page.route("**/plan/api/places?query=*", (route) => {
      const query = new URL(route.request().url()).searchParams.get("query") ?? "";
      const place = query.startsWith("kood")
        ? {
            id: TO,
            type: "island",
            name: { en: "Kooddoo" },
            geo: { lat: 0.73, lng: 73.43 },
            isHub: false,
          }
        : {
            id: FROM,
            type: "island",
            name: { en: "Maafushi" },
            geo: { lat: 3.94, lng: 73.49 },
            isHub: false,
          };
      return route.fulfill({ json: { items: [place], page: { limit: 8 } } });
    });
    await page.goto("/plan");
    await expect(page.getByRole("heading", { level: 1 })).toHaveCount(1);
    await expectNoA11yViolations(page);

    await page.getByRole("button", { name: "Search trips" }).click();
    await slowExpect(page.getByText("Choose where you are leaving from.")).toBeVisible();

    const from = page.getByRole("combobox", { name: "From" });
    await from.fill("maa");
    await page
      .getByRole("option", { name: /Maafushi/ })
      .first()
      .click();
    const to = page.getByRole("combobox", { name: "To" });
    await to.fill("kood");
    await slowExpect(page.getByRole("option", { name: /Maafushi|Kooddoo/ })).toBeVisible();
    await to.press("ArrowDown");
    await to.press("Enter");
    await page.getByRole("button", { name: /Date/ }).click();
    await page
      .getByRole("dialog")
      .getByRole("button", { name: /^\w+, \w+ \d+/ })
      .last()
      .click();
    await page.getByLabel("Time", { exact: true }).fill("17:00");
    await page.getByRole("radio", { name: "Arrive by" }).click();
    await page.getByRole("button", { name: "Search trips" }).click();

    await slowExpect(page).toHaveURL(/\/results\?/);
    await slowExpect(page).toHaveURL(/mode=arriveBy/);
    await slowExpect(page.getByRole("article").first()).toBeVisible();
    await slowExpect(
      page
        .getByRole("listitem")
        .filter({ hasText: /^Safest$/ })
        .first(),
    ).toBeVisible();
    await expectNoA11yViolations(page);

    await page
      .getByRole("link", { name: /View trip/ })
      .first()
      .click();
    await slowExpect(page).toHaveURL(/\/trip\/trp_/);
    await slowExpect(page.getByRole("heading", { name: "Why this trip" })).toBeVisible();
    await slowExpect(page.getByRole("heading", { name: "Price breakdown" })).toBeVisible();
    await slowExpect(page.getByRole("region", { name: "Map of your route" })).toBeVisible();
    await expectNoA11yViolations(page);

    const book = page.getByRole("link", { name: /Book this trip/ });
    await slowExpect(book).toHaveAttribute("href", /\/book\?option=trp_.*adults=\d+&children=\d+&infants=\d+/);

    await page.getByRole("link", { name: /Back to results/ }).click();
    await slowExpect(page).toHaveURL(/\/results\?/);
  });

  test("results are shareable via the URL and compare works", async ({ page }) => {
    await page.goto(RESULTS);
    await slowExpect(page.getByRole("article").first()).toBeVisible();
    await page
      .getByRole("checkbox", { name: /^Compare/ })
      .first()
      .check();
    await slowExpect(page.getByText("1 trip selected")).toBeVisible();
  });

  test("incomplete search shows guidance instead of calling the API", async ({ page }) => {
    await page.goto("/results");
    await slowExpect(page.getByRole("heading", { name: "Finish your search" })).toBeVisible();
    await expectNoA11yViolations(page);
  });

  test("API failure shows a recoverable error state", async ({ page }) => {
    await page.route("**/plan/api/search", (route) =>
      route.fulfill({ status: 502, json: { code: "UPSTREAM_UNAVAILABLE" } }),
    );
    await page.goto(RESULTS);
    await slowExpect(
      page.getByRole("alert").filter({ hasText: "We could not plan this trip" }),
    ).toBeVisible();
    await page.unroute("**/plan/api/search");
    await page.getByRole("button", { name: "Try again" }).click();
    await slowExpect(page.getByRole("article").first()).toBeVisible();
  });

  test("no results offers things to try", async ({ page }) => {
    await page.route("**/plan/api/search", (route) =>
      route.fulfill({ json: { searchId: "srch_01K8Z3M5Q7R9T2V4W6X8Y0F1G2", options: [] } }),
    );
    await page.goto(RESULTS);
    await slowExpect(page.getByRole("heading", { name: "No safe trip found" })).toBeVisible();
    await slowExpect(page.getByRole("button", { name: "Try leaving later" })).toBeVisible();
    await expectNoA11yViolations(page);
  });

  test("expired trip shows a not-found state", async ({ page }) => {
    await page.route("**/plan/api/options/*", (route) =>
      route.fulfill({ status: 404, json: { code: "NOT_FOUND" } }),
    );
    await page.goto("/trip/trp_01K8Z3M5Q7R9T2V4W6X8Y0E1F2");
    await slowExpect(
      page.getByRole("heading", { name: "This trip is no longer available" }),
    ).toBeVisible();
  });

  test("unknown trip id is a real HTTP 404", async ({ request }) => {
    const res = await request.get("/trip/abc", { headers: { cookie: "dh_rt=e2e" } });
    expect(res.status()).toBe(404);
  });
});

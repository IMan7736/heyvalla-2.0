import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

const sections = [
  "colour",
  "type",
  "icons",
  "buttons",
  "forms",
  "navigation",
  "overlays",
  "feedback",
  "journey",
  "ticket",
  "states",
  "elevation",
] as const;

async function ready(page: Page, path: string) {
  await page.goto(path);
  await page.evaluate(() => document.fonts.ready);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
}

async function expectNoA11yViolations(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"])
    .analyze();
  expect(results.violations.map((v) => `${v.id}: ${v.nodes[0]?.target.join(" ")}`)).toEqual([]);
}

test.describe("design system gallery", () => {
  test("has no detectable accessibility violations", async ({ page }) => {
    await ready(page, "/dev/ui");
    await expectNoA11yViolations(page);
  });

  for (const id of sections) {
    test(`visual baseline: ${id}`, async ({ page }) => {
      await ready(page, "/dev/ui");
      const section = page.locator(`section[aria-labelledby="g-${id}"]`);
      await section.scrollIntoViewIfNeeded();
      await expect(section).toHaveScreenshot(`${id}.png`);
    });
  }
});

test.describe("landing page", () => {
  test("renders the hero, search form and sections without axe violations", async ({ page }) => {
    await ready(page, "/");
    await expect(page.getByRole("button", { name: "Search trips" })).toBeVisible();
    await expectNoA11yViolations(page);
  });

  test("has no horizontal scroll at the viewport width", async ({ page }) => {
    await ready(page, "/");
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow).toBeLessThanOrEqual(0);
  });

  test("404 page offers a way back", async ({ page }) => {
    await page.goto("/definitely-not-a-page");
    await expect(page.getByRole("link", { name: "Plan a trip" }).first()).toBeVisible();
  });
});

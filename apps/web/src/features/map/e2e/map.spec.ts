import { expect, test, type Page } from "@playwright/test";

const ready = (page: Page) => page.waitForSelector("[data-map-ready=true]", { timeout: 60_000 });

test("trip map loads, draws stops and a vehicle, and the scrubber moves it", async ({ page }) => {
  await page.goto("/?view=trip");
  await ready(page);
  await expect(page.getByTestId("map-stop")).toHaveCount(5);
  await expect(page.getByText("Routes are illustrative, not for navigation.")).toBeVisible();

  const scrubber = page.getByTestId("map-scrubber");
  const before = await scrubber.getAttribute("aria-valuetext");
  expect(before).toContain("journey not started");
  await scrubber.fill("100");
  await expect(scrubber).toHaveAttribute("aria-valuetext", /Ferry towards|Taxi towards|waiting at/);
  await expect(page.getByTestId("map-vehicle")).toHaveAttribute("data-phase", /moving|waiting/);
  await scrubber.fill("425");
  await expect(scrubber).toHaveAttribute("aria-valuetext", /arrived at/);
});

test("play button toggles playback and is keyboard reachable", async ({ page }) => {
  await page.goto("/?view=trip");
  await ready(page);
  const play = page.getByTestId("map-play");
  await play.focus();
  await page.keyboard.press("Enter");
  await expect(play).toHaveAccessibleName("Pause journey");
  await page.keyboard.press("Enter");
  await expect(play).toHaveAccessibleName("Play journey");
});

test("falls back to a journey list when WebGL is unavailable", async ({ page }) => {
  await page.addInitScript(() => {
    const original = HTMLCanvasElement.prototype.getContext;
    const patched = function (this: HTMLCanvasElement, type: string, ...rest: unknown[]) {
      if (type.startsWith("webgl")) return null;
      return (original as (this: HTMLCanvasElement, ...a: unknown[]) => unknown).call(
        this,
        type,
        ...rest,
      );
    };
    HTMLCanvasElement.prototype.getContext = patched as unknown as typeof original;
  });
  await page.goto("/?view=trip");
  await expect(page.getByTestId("map-fallback")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("list", { name: "Journey steps" }).getByRole("listitem")).toHaveCount(
    4,
  );
});

test("network map: search flies to an island and shows the selection", async ({ page }) => {
  await page.goto("/?view=network");
  await ready(page);
  const box = page.getByRole("combobox", { name: "Find an island" });
  await box.fill("kulhu");
  await expect(page.getByRole("option", { name: /Kulhudhuffushi/ }).first()).toBeVisible();
  await box.press("Enter");
  await expect(
    page.getByRole("status").filter({ hasText: "Selected: Kulhudhuffushi" }),
  ).toBeVisible();
});

test("map picker: choosing by search reports the selection and can be cleared", async ({
  page,
}) => {
  await page.goto("/?view=picker");
  await ready(page);
  const box = page.getByRole("combobox", { name: "From" });
  await box.fill("maafushi");
  await box.press("Enter");
  await expect(page.getByText("Selected: Maafushi")).toBeVisible();
  await page.getByRole("button", { name: "Clear" }).click();
  await expect(page.getByText("No place selected")).toBeVisible();
});

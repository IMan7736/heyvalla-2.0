import { expect, test, type ConsoleMessage } from "@playwright/test";

/** Shortcuts are only live after hydration; MotionProvider flags that on <html>. */
async function ready(page: import("@playwright/test").Page): Promise<void> {
  await page.locator("html[data-motion-ready='true']").waitFor({ state: "attached" });
}

function collectErrors(page: import("@playwright/test").Page): string[] {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (msg: ConsoleMessage) => {
    if (msg.type() === "error") errors.push(msg.text());
  });
  return errors;
}

test.describe("global interaction layer", () => {
  test("home renders with the provider mounted and no runtime errors", async ({ page }) => {
    const errors = collectErrors(page);
    const response = await page.goto("/");
    expect(response?.status()).toBe(200);
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    expect(errors).toEqual([]);
  });

  test("palette opens with Ctrl+K, traps focus and restores it on Escape", async ({ page }) => {
    await page.goto("/");
    await ready(page);
    await page.evaluate(() => {
      const probe = document.createElement("button");
      probe.id = "focus-probe";
      probe.textContent = "probe";
      document.body.prepend(probe);
      probe.focus();
    });
    await page.keyboard.press("Control+k");
    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();
    await expect(page.getByRole("dialog").getByRole("combobox")).toBeFocused();

    for (let i = 0; i < 6; i++) {
      await page.keyboard.press("Tab");
      const inside = await page.evaluate(() =>
        Boolean(document.activeElement?.closest("[role='dialog']")),
      );
      expect(inside).toBe(true);
    }

    await page.keyboard.press("Escape");
    await expect(dialog).toBeHidden();
    await expect(page.locator("#focus-probe")).toBeFocused();
  });

  test("choosing a destination closes the palette and leaves focus on a live element", async ({
    page,
  }) => {
    await page.goto("/");
    await ready(page);
    await page.keyboard.press("Control+k");
    await page.getByRole("option", { name: /Home/ }).click();
    await expect(page.getByRole("dialog")).toBeHidden();
    const connected = await page.evaluate(() => document.activeElement?.isConnected ?? false);
    expect(connected).toBe(true);
    expect(await page.evaluate(() => window.scrollY)).toBe(0);
  });

  test("? shows the shortcut list and the back button returns to search", async ({ page }) => {
    await page.goto("/");
    await ready(page);
    await page.keyboard.press("?");
    await expect(page.getByRole("heading", { name: "Keyboard shortcuts" })).toBeVisible();
    await page.getByRole("button", { name: "Back to search" }).click();
    await expect(page.getByRole("dialog").getByRole("combobox")).toBeVisible();
  });

  test("shortcuts are ignored while typing in a field", async ({ page }) => {
    await page.goto("/");
    await ready(page);
    await page.keyboard.press("Control+k");
    await page.getByRole("dialog").getByRole("combobox").fill("?");
    await expect(page.getByRole("heading", { name: "Keyboard shortcuts" })).toBeHidden();
  });
});

test.describe("reduced motion", () => {
  test("motion CSS drops all travel and the palette appears in its final state", async ({
    page,
  }) => {
    await page.emulateMedia({ reducedMotion: "reduce" });
    await page.goto("/");
    await ready(page);

    const vars = await page.evaluate(() => {
      const style = getComputedStyle(document.documentElement);
      return {
        travel: style.getPropertyValue("--motion-travel").trim(),
        exit: style.getPropertyValue("--motion-exit").trim(),
      };
    });
    expect(vars).toEqual({ travel: "0px", exit: "0s" });

    await page.keyboard.press("Control+k");
    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();
    // Final state immediately: fully opaque, no transform on the animated wrapper.
    const wrapper = dialog.locator("> div").first();
    await expect(wrapper).toHaveCSS("opacity", "1");
    await expect(wrapper).toHaveCSS("transform", "none");
  });

  test("full-motion mode keeps the real travel distance", async ({ page }) => {
    await page.emulateMedia({ reducedMotion: "no-preference" });
    await page.goto("/");
    const travel = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue("--motion-travel").trim(),
    );
    expect(travel).toBe("24px");
  });
});

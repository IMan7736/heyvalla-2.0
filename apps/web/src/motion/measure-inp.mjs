// Interaction latency check for the global layer. Usage (dev or prod server running):
//   PORT=3109 node src/motion/measure-inp.mjs
// Opens the palette, types, navigates the list and closes it, then reports the worst Event
// Timing duration (the INP basis). Exits 1 if it is >= BUDGET_MS. Run against `next start`
// for a number that reflects production; dev builds are slower and only an upper bound.
import { chromium } from "@playwright/test";

const BUDGET_MS = 200;
const port = process.env.PORT ?? "3000";
const base = process.env.BASE_URL ?? `http://localhost:${port}`;

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
await page.goto(base);
await page.getByRole("heading", { level: 1 }).waitFor();

await page.evaluate(() => {
  window.__inp = [];
  new PerformanceObserver((list) => {
    for (const entry of list.getEntries()) {
      if (entry.interactionId) window.__inp.push({ name: entry.name, duration: entry.duration });
    }
  }).observe({ type: "event", durationThreshold: 16, buffered: true });
});

// Machine-noise baseline: interactions that do no work of ours. If this is already slow, the
// host is overloaded and the palette numbers below are not meaningful.
await page.evaluate(() => (window.__inp.length = 0));
for (let i = 0; i < 8; i++) {
  await page.keyboard.press("Tab");
  await page.evaluate(() => new Promise((r) => setTimeout(r, 60)));
}
const baseline = await page.evaluate(() =>
  window.__inp.reduce((m, s) => Math.max(m, s.duration), 0),
);
console.log(`baseline (Tab presses, no palette): ${baseline.toFixed(0)} ms`);
await page.evaluate(() => (window.__inp.length = 0));
await page.evaluate(() => document.activeElement?.blur());

const wait = () => page.evaluate(() => new Promise((r) => setTimeout(r, 150)));
for (let round = 0; round < 3; round++) {
  await page.keyboard.press("Control+k");
  await page.getByRole("dialog").getByRole("combobox").waitFor();
  await page.keyboard.type("maafushi", { delay: 30 });
  await wait();
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Escape");
  await wait();
  await page.keyboard.press("?");
  await page.getByRole("button", { name: "Back to search" }).click();
  await page.keyboard.press("Escape");
  await wait();
}

const samples = await page.evaluate(() => window.__inp);
await browser.close();

const worst = samples.reduce((max, s) => Math.max(max, s.duration), 0);
console.log(`interactions measured: ${samples.length}`);
const byName = new Map();
for (const s of samples) byName.set(s.name, Math.max(byName.get(s.name) ?? 0, s.duration));
console.log([...byName].map(([name, ms]) => `  ${name}: ${ms.toFixed(0)} ms`).join("\n"));
console.log(`worst interaction: ${worst.toFixed(0)} ms (budget ${BUDGET_MS} ms)`);
if (samples.length === 0) {
  console.error("no interactions were recorded; the check is not valid");
  process.exit(1);
}
if (worst >= BUDGET_MS) process.exit(1);

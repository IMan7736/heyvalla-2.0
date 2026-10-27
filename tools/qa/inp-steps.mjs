// LOOKOUT: per-step interaction latency on a production server (not an edit of TIDE's script).
//   BASE_URL=http://localhost:3110/help node tools/qa/inp-steps.mjs
// Reports each step's Event Timing duration for the cold first open and the warm repeats, plus INP
// as defined for the page (p98 of interactions when more than 50, otherwise the worst).
import { createRequire } from "node:module";
const require = createRequire(new URL("../../apps/web/package.json", import.meta.url));
const { chromium } = require("@playwright/test");

const base = process.env.BASE_URL ?? "http://localhost:3110/help";
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
await page.goto(base);
await page.getByRole("heading", { level: 1 }).first().waitFor();
await page.waitForLoadState("networkidle");
await page.evaluate(() => {
  window.__ev = [];
  new PerformanceObserver((l) => {
    for (const e of l.getEntries())
      if (e.interactionId)
        window.__ev.push({ n: e.name, d: e.duration, delay: e.processingStart - e.startTime, proc: e.processingEnd - e.processingStart });
  }).observe({ type: "event", durationThreshold: 16, buffered: true });
});
const take = async (label, fn) => {
  await page.evaluate(() => (window.__ev = []));
  await fn();
  await page.evaluate(() => new Promise((r) => setTimeout(r, 200)));
  const ev = await page.evaluate(() => window.__ev);
  const worst = ev.reduce((m, e) => (e.d > m.d ? e : m), { d: 0, n: "-", delay: 0, proc: 0 });
  console.log(`${label.padEnd(26)} worst ${String(worst.d).padStart(4)} ms  (${worst.n}, input delay ${worst.delay.toFixed(0)}, processing ${worst.proc.toFixed(0)})`);
  return ev;
};
const all = [];
for (let round = 0; round < 4; round++) {
  const tag = round === 0 ? "COLD" : `warm${round}`;
  all.push(...(await take(`${tag} open Ctrl+K`, async () => { await page.keyboard.press("Control+k"); await page.getByRole("dialog").getByRole("combobox").waitFor(); })));
  all.push(...(await take(`${tag} type`, () => page.keyboard.type("maafushi", { delay: 40 }))));
  all.push(...(await take(`${tag} arrows`, async () => { await page.keyboard.press("ArrowDown"); await page.keyboard.press("ArrowDown"); })));
  all.push(...(await take(`${tag} Escape`, () => page.keyboard.press("Escape"))));
}
// Ordinary page interactions on /help: expand FAQ items by click and keyboard.
const triggers = page.locator("button[aria-expanded]");
const n = await triggers.count();
for (let i = 0; i < Math.min(n, 4); i++) all.push(...(await take(`faq click ${i}`, () => triggers.nth(i).click())));
await page.keyboard.press("Tab");
all.push(...(await take("faq keyboard Enter", () => page.keyboard.press("Enter"))));
const d = all.map((e) => e.d).sort((a, b) => a - b);
const inp = d.length > 50 ? d[Math.min(d.length - 1, Math.ceil(d.length * 0.98) - 1)] : d[d.length - 1];
console.log(`\ninteractions ${d.length}, worst ${d.at(-1)} ms, INP(p98 rule) ${inp} ms  (good < 200, poor > 500)`);
await browser.close();

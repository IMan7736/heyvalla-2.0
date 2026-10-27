// LOOKOUT re-verify L-04: per-event detail for the first Ctrl+K at a given delay after load. DELAY=3000 node tools/qa/inp-detail.mjs
import { createRequire } from "node:module";
const require = createRequire(new URL("../../apps/web/package.json", import.meta.url));
const { chromium } = require("@playwright/test");
const base = process.env.BASE_URL ?? "http://localhost:3110/help", delay = +(process.env.DELAY ?? 3000), mode = process.env.WAIT ?? "load";
const b = await chromium.launch();
for (let i = 0; i < 3; i++) {
  const ctx = await b.newContext({ viewport: { width: 1440, height: 900 } }); const p = await ctx.newPage();
  await p.addInitScript(() => { window.__ev = []; new PerformanceObserver((l) => { for (const e of l.getEntries()) window.__ev.push({ n: e.name, d: e.duration, id: e.interactionId, t: Math.round(e.startTime), proc: Math.round(e.processingEnd - e.processingStart) }); }).observe({ type: "event", durationThreshold: 16, buffered: true }); });
  await p.goto(base, { waitUntil: mode }); await p.waitForTimeout(delay);
  const now = await p.evaluate(() => Math.round(performance.now()));
  await p.keyboard.press("Control+k"); await p.getByRole("dialog").getByRole("combobox").waitFor(); await p.waitForTimeout(300);
  console.log(`run ${i + 1}: key pressed at page time ~${now} ms ->`, JSON.stringify(await p.evaluate(() => window.__ev)));
  await ctx.close();
}
await b.close();

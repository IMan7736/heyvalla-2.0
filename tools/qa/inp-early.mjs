// LOOKOUT re-verify L-04: Ctrl+K pressed at fixed delays after load, i.e. before/after the palette pre-warm (about 1.2 s + idle).
import { createRequire } from "node:module";
const require = createRequire(new URL("../../apps/web/package.json", import.meta.url));
const { chromium } = require("@playwright/test");
const base = process.env.BASE_URL ?? "http://localhost:3110/help";
const b = await chromium.launch();
for (const delay of [0, 300, 800, 1500, 3000]) {
  const runs = [];
  for (let i = 0; i < 3; i++) {
    const ctx = await b.newContext({ viewport: { width: 1440, height: 900 } }); const p = await ctx.newPage();
    await p.addInitScript(() => { window.__ev = []; new PerformanceObserver((l) => { for (const e of l.getEntries()) if (e.interactionId) window.__ev.push(e.duration); }).observe({ type: "event", durationThreshold: 16, buffered: true }); });
    await p.goto(base, { waitUntil: "load" });
    await p.waitForTimeout(delay);
    const t0 = Date.now(); await p.keyboard.press("Control+k"); const opened = await p.getByRole("dialog").getByRole("combobox").waitFor({ timeout: 5000 }).then(() => true, () => false); const wall = Date.now() - t0;
    await p.waitForTimeout(200); const ev = await p.evaluate(() => window.__ev);
    runs.push(opened ? `${Math.max(0, ...ev)}ms(wall ${wall})` : "DID-NOT-OPEN"); await ctx.close();
  }
  console.log(`Ctrl+K at +${String(delay).padStart(4)} ms after heading visible: ${runs.join("  ")}`);
}
await b.close();

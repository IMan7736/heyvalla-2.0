// LOOKOUT re-verify L-04: user-perceived latency of the FIRST Ctrl+K = keydown timeStamp -> dialog painted (2 rAFs after it
// appears in the DOM). Independent of Event Timing, which drops entries while the palette pre-warm is running.
//   BASE_URL=http://localhost:3110/help node tools/qa/open-latency.mjs
import { createRequire } from "node:module";
const require = createRequire(new URL("../../apps/web/package.json", import.meta.url));
const { chromium } = require("@playwright/test");
const base = process.env.BASE_URL ?? "http://localhost:3110/help";
const b = await chromium.launch();
console.log("settle = ms after the load event before pressing Ctrl+K (palette pre-warm starts ~1200 ms + idle)");
for (const settle of [0, 300, 700, 1000, 1300, 1600, 2000, 3000, 6000]) {
  const out = [];
  for (let i = 0; i < 4; i++) {
    const ctx = await b.newContext({ viewport: { width: 1440, height: 900 } }); const p = await ctx.newPage();
    await p.addInitScript(() => {
      window.__lat = null;
      addEventListener("keydown", (e) => {
        if (!(e.ctrlKey && e.key === "k") || window.__lat !== null) return;
        const t0 = e.timeStamp;
        const mo = new MutationObserver(() => { if (document.querySelector('[role="dialog"] [role="combobox"]')) { mo.disconnect(); requestAnimationFrame(() => requestAnimationFrame(() => (window.__lat = Math.round(performance.now() - t0)))); } });
        mo.observe(document, { childList: true, subtree: true });
      }, true);
    });
    await p.goto(base, { waitUntil: "load" }); await p.waitForTimeout(settle);
    await p.keyboard.press("Control+k");
    const lat = await p.waitForFunction(() => window.__lat, null, { timeout: 8000 }).then((h) => h.jsonValue(), () => "never");
    out.push(lat); await ctx.close();
  }
  console.log(`settle ${String(settle).padStart(4)} ms -> ${out.join(" ms, ")} ms`);
}
await b.close();

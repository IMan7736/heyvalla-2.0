// LOOKOUT re-verify L-04: A/B of harness differences for the first Ctrl+K on /help (production build).
//   ctx=page|context  observer=init|late  settle=<ms after load>
import { createRequire } from "node:module";
const require = createRequire(new URL("../../apps/web/package.json", import.meta.url));
const { chromium } = require("@playwright/test");
const base = process.env.BASE_URL ?? "http://localhost:3110/help";
const OBS = () => { window.__ev = []; new PerformanceObserver((l) => { for (const e of l.getEntries()) if (e.interactionId && e.name === "keydown") window.__ev.push(Math.round(e.duration)); }).observe({ type: "event", durationThreshold: 16, buffered: true }); };
const b = await chromium.launch();
for (const ctxKind of ["page", "context"]) for (const observer of ["init", "late"]) for (const settle of [2500, 6000]) {
  const out = [];
  for (let i = 0; i < 3; i++) {
    let page, ctx;
    if (ctxKind === "page") page = await b.newPage({ viewport: { width: 1440, height: 900 } }); else { ctx = await b.newContext({ viewport: { width: 1440, height: 900 } }); page = await ctx.newPage(); }
    if (observer === "init") await page.addInitScript(OBS);
    await page.goto(base); await page.getByRole("heading", { level: 1 }).first().waitFor();
    if (observer === "late") await page.evaluate(OBS);
    await page.waitForTimeout(settle);
    await page.keyboard.press("Control+k"); await page.getByRole("dialog").getByRole("combobox").waitFor(); await page.waitForTimeout(250);
    out.push((await page.evaluate(() => window.__ev))[0] ?? "none"); await (ctx ?? page).close();
  }
  console.log(`${ctxKind.padEnd(8)} observer=${observer.padEnd(5)} settle=${String(settle).padStart(4)} ms -> first keydown: ${out.join(", ")}`);
}
await b.close();

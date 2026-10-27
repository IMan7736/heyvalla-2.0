// LOOKOUT: screenshots of the traveller journey at 1280 and 1440. OUT=<dir> COOKIES=<jar> node tools/qa/shots.mjs
import { createRequire } from "node:module";
import { readFileSync } from "node:fs";
const require = createRequire(new URL("../../apps/web/package.json", import.meta.url));
const { chromium } = require("@playwright/test");
const base = process.env.BASE_URL ?? "http://localhost:3110", out = process.env.OUT ?? ".";
const cookies = readFileSync(process.env.COOKIES, "utf8").split("\n").filter((l) => l.startsWith("#HttpOnly_")).map((l) => l.replace("#HttpOnly_", "").split("\t")).map((c) => ({ name: c[5], value: c[6], domain: c[0], path: "/", httpOnly: true }));
const q = process.env.RESULTS_QUERY, trip = process.env.TRIP_ID;
const b = await chromium.launch();
for (const w of [1280]) {
  const ctx = await b.newContext({ viewport: { width: w, height: 800 } }); await ctx.addCookies(cookies);
  const p = await ctx.newPage(); const errs = [];
  p.on("console", (m) => m.type() === "error" && errs.push(m.text().slice(0, 120))); p.on("pageerror", (e) => errs.push("PAGEERROR " + e.message.slice(0, 120)));
  for (const [name, url] of [["home", "/"], ["results", `/results?${q}`], ["trip", `/trip/${trip}?${q}`], ["trips", "/trips"], ["account", "/account"]]) {
    await p.goto(base + url, { waitUntil: "networkidle" }).catch(() => {}); await p.waitForTimeout(800);
    await p.screenshot({ path: `${out}/${name}-${w}.png` });
    console.log(w, name, "hscroll", await p.evaluate(() => document.documentElement.scrollWidth > innerWidth));
  }
  console.log("console errors:", [...new Set(errs)]);
}
await b.close();

// LOOKOUT: axe-core on every route at 1440 and 1280, anonymous and signed in.
//   COOKIES=<netscape jar> BASE_URL=http://localhost:3110 node tools/qa/axe-all.mjs
import { createRequire } from "node:module";
import { readFileSync } from "node:fs";
const require = createRequire(new URL("../../apps/web/package.json", import.meta.url));
const { chromium } = require("@playwright/test");
const AxeBuilder = require("@axe-core/playwright").default;

const base = process.env.BASE_URL ?? "http://localhost:3110";
const jar = process.env.COOKIES;
const cookies = jar
  ? readFileSync(jar, "utf8").split("\n").filter((l) => l && (!l.startsWith("#") || l.startsWith("#HttpOnly_")))
      .map((l) => l.replace("#HttpOnly_", "").split("\t"))
      .filter((c) => c.length >= 7)
      .map((c) => ({ name: c[5], value: c[6], domain: c[0], path: "/", httpOnly: true }))
  : [];
const routes = (process.env.ROUTES ?? "/,/plan,/results,/help,/login,/signup,/forgot-password,/reset-password,/verify-email,/legal/terms,/legal/privacy,/does-not-exist,/trips,/account,/book").split(",");
const browser = await chromium.launch();
let total = 0;
for (const width of [1440, 1280]) {
  const ctx = await browser.newContext({ viewport: { width, height: 900 } });
  if (cookies.length) await ctx.addCookies(cookies);
  const page = await ctx.newPage();
  for (const r of routes) {
    const resp = await page.goto(base + r, { waitUntil: "networkidle" }).catch((e) => ({ status: () => `ERR ${e.message.slice(0, 40)}`, url: () => "" }));
    const hs = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth).catch(() => null);
    const res = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa", "best-practice"]).analyze();
    const v = res.violations;
    total += v.length;
    console.log(`${width} ${r.padEnd(18)} ${String(resp.status()).padEnd(4)} -> ${page.url().replace(base, "").padEnd(22)} hscroll=${hs} violations=${v.length}`);
    for (const x of v) console.log(`     [${x.impact}] ${x.id}: ${x.nodes.length}x  e.g. ${x.nodes[0].target.join(" ")}  | ${x.help}`);
  }
  await ctx.close();
}
console.log("total violations", total);
await browser.close();

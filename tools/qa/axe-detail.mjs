// LOOKOUT: print the failing nodes for chosen axe rules. COOKIES optional (Netscape jar).
import { createRequire } from "node:module";
import { readFileSync } from "node:fs";
const require = createRequire(new URL("../../apps/web/package.json", import.meta.url));
const { chromium } = require("@playwright/test");
const AxeBuilder = require("@axe-core/playwright").default;
const base = process.env.BASE_URL ?? "http://localhost:3110";
const cookies = process.env.COOKIES
  ? readFileSync(process.env.COOKIES, "utf8").split("\n").filter((l) => l.startsWith("#HttpOnly_")).map((l) => l.replace("#HttpOnly_", "").split("\t"))
      .map((c) => ({ name: c[5], value: c[6], domain: c[0], path: "/", httpOnly: true }))
  : [];
const rules = (process.env.RULES ?? "label-content-name-mismatch,target-size,page-has-heading-one").split(",");
const b = await chromium.launch();
const c = await b.newContext({ viewport: { width: 1440, height: 900 } });
if (cookies.length) await c.addCookies(cookies);
const p = await c.newPage();
for (const r of (process.env.ROUTES ?? "/help").split(",")) {
  await p.goto(base + r, { waitUntil: "networkidle" });
  const res = await new AxeBuilder({ page: p }).withRules(rules).analyze();
  for (const v of res.violations) for (const n of v.nodes.slice(0, 2)) console.log(r, v.id, "\n  ", n.html.slice(0, 300), "\n  ", (n.any[0]?.message ?? "").slice(0, 200));
}
await b.close();

// LOOKOUT re-verify L-01: parallel navigations after dh_at lapses (two tabs / prefetch), then a dead refresh cookie.
import { createRequire } from "node:module";
const require = createRequire(new URL("../../apps/web/package.json", import.meta.url));
const { chromium } = require("@playwright/test");
const base = process.env.BASE_URL ?? "http://localhost:3110";
const b = await chromium.launch(); const ctx = await b.newContext({ viewport: { width: 1280, height: 800 } });
await ctx.request.post(base + "/api/auth/login", { headers: { Origin: base }, data: { email: process.env.EMAIL, password: process.env.PASS } });
const signedIn = async (p) => (await p.locator("header").first().innerText()).includes("LT");
let bad = 0;
for (let round = 1; round <= 5; round++) {
  await ctx.clearCookies({ name: "dh_at" });
  const pages = await Promise.all([ctx.newPage(), ctx.newPage(), ctx.newPage()]);
  const res = await Promise.all(pages.map((p, i) => p.goto(base + ["/help", "/trips", "/account"][i], { waitUntil: "networkidle" })));
  const states = await Promise.all(pages.map(async (p) => `${p.url().replace(base, "")}:${await signedIn(p)}`));
  const ok = states.every((s) => s.endsWith(":true") && !s.includes("/login"));
  if (!ok) bad++;
  console.log(`round ${round} parallel x3 after dh_at drop ->`, states.join("  "), ok ? "OK" : "FAIL");
  await Promise.all(pages.map((p) => p.close()));
}
// sequential session still alive after the races?
const p = await ctx.newPage(); await ctx.clearCookies({ name: "dh_at" }); await p.goto(base + "/account", { waitUntil: "networkidle" });
console.log("after races, /account ->", p.url().replace(base, ""), "signed in:", await signedIn(p).catch(() => false));
// dead refresh cookie
await ctx.clearCookies({ name: "dh_at" }); await ctx.clearCookies({ name: "dh_rt" });
await ctx.addCookies([{ name: "dh_rt", value: "rt_deadbeefdeadbeef", url: base, httpOnly: true }]);
await p.goto(base + "/account", { waitUntil: "networkidle" });
console.log("dead refresh, /account ->", p.url().replace(base, ""), "cookies:", (await ctx.cookies()).map((c) => c.name).join(",") || "(none)");
await p.goto(base + "/help"); console.log("dead refresh, /help header signed-out:", (await p.locator("header").first().innerText()).includes("Sign in"));
console.log(bad ? `RACE FAILURES: ${bad}` : "no race failures");
await b.close();

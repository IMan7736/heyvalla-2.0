// LOOKOUT: real-browser check of a traveller whose access-token cookie has expired (browser dropped dh_at) but whose
// refresh cookie is still valid. One persistent context so rotated cookies are kept (no replay of old tokens).
//   EMAIL=... PASS=... node tools/qa/expired-at.mjs
import { createRequire } from "node:module";
const require = createRequire(new URL("../../apps/web/package.json", import.meta.url));
const { chromium } = require("@playwright/test");
const base = process.env.BASE_URL ?? "http://localhost:3110";
const b = await chromium.launch(); const ctx = await b.newContext({ viewport: { width: 1280, height: 800 } }); const p = await ctx.newPage();
const state = async (label) => {
  const header = await p.locator("header").first().innerText().then((t) => t.replace(/\s+/g, " ").slice(0, 90)).catch(() => "?");
  const names = (await ctx.cookies()).map((c) => c.name).sort().join(",");
  console.log(label.padEnd(34), "| url", p.url().replace(base, "").padEnd(24), "| cookies", names.padEnd(12), "| header:", header);
};
await p.goto(base + "/login");
const r = await ctx.request.post(base + "/api/auth/login", { headers: { Origin: base }, data: { email: process.env.EMAIL, password: process.env.PASS } });
console.log("login", r.status());
await p.goto(base + "/help"); await state("fresh session /help");
await ctx.clearCookies({ name: "dh_at" });
await p.goto(base + "/help"); await state("dh_at dropped, /help");
await p.goto(base + "/help"); await state("same again, /help");
await p.goto(base + "/trips", { waitUntil: "networkidle" }); await state("/trips");
const body = (await p.locator("main").innerText()).replace(/\s+/g, " ").slice(0, 140); console.log("   /trips main:", body);
await ctx.clearCookies({ name: "dh_at" });
await p.goto(base + "/account", { waitUntil: "networkidle" }); await state("dh_at dropped, /account");
console.log("   /account main:", (await p.locator("main").innerText()).replace(/\s+/g, " ").slice(0, 140));
await ctx.clearCookies({ name: "dh_at" });
await p.goto(base + "/results?from=apt_3DB5FSPTX9JBM1FVZZWJD2HWAK&to=isl_3J4M7RAMSYA0GV00MYVA92KY3S&date=2026-10-30&time=08%3A00&mode=departAfter&adults=1", { waitUntil: "networkidle" }); await state("dh_at dropped, /results");
await b.close();

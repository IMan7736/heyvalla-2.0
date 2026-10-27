// Compares the operations a running API serves (its generated OpenAPI) with the contract bundle.
//   node scripts/api-drift.mjs [http://localhost:5100]      exits 1 when they differ
// Needs the API running in Development (MapOpenApi is dev only).
import { readFileSync } from "node:fs";

const base = process.argv[2] ?? "http://localhost:5100";
const methods = ["get", "post", "put", "patch", "delete"];
const operations = (doc) =>
  new Set(
    Object.entries(doc.paths).flatMap(([p, item]) =>
      methods.filter((m) => item[m]).map((m) => `${m.toUpperCase()} ${p.replace(/^\/v1/, "")}`),
    ),
  );

const served = operations(await (await fetch(`${base}/openapi/v1.json`)).json());
const contract = operations(
  JSON.parse(readFileSync(new URL("../generated/openapi.bundled.json", import.meta.url), "utf8")),
);
const missing = [...contract].filter((o) => !served.has(o)).sort();
const extra = [...served].filter((o) => !contract.has(o)).sort();

if (missing.length)
  console.log(`In contract but NOT served:\n${missing.map((o) => `  ${o}`).join("\n")}`);
if (extra.length)
  console.log(`Served but NOT in contract:\n${extra.map((o) => `  ${o}`).join("\n")}`);
if (!missing.length && !extra.length) console.log("API and contract agree.");
process.exitCode = missing.length || extra.length ? 1 : 0;

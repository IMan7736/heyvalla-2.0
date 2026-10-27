// Builds the single bundled OpenAPI document from the per-module files.
//   node scripts/bundle.mjs           -> writes generated/openapi.bundled.{yaml,json}
//   node scripts/bundle.mjs --check   -> fails if the committed bundle differs from a fresh one
import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, rmSync, writeFileSync, existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import YAML from "yaml";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const specDir = join(root, "openapi");
const outDir = join(root, "generated");
const check = process.argv.includes("--check");

// Module files, in the order their tags appear in the docs.
const modules = [
  { file: "meta.yaml", tag: "Meta", description: "Platform endpoints" },
  { file: "auth.yaml", tag: "Auth", description: "Registration, login and token rotation" },
  { file: "auth.yaml", tag: "Account", description: "Profile and saved travellers" },
  { file: "catalog.yaml", tag: "Catalog", description: "Places, operators, services and status" },
  { file: "trips.yaml", tag: "Trips", description: "Door-to-door trip planning" },
  { file: "geo.yaml", tag: "Geo", description: "Route geometry (approximate)" },
  { file: "bookings.yaml", tag: "Bookings", description: "Holds, payment, tickets" },
];

const escapePointer = (s) => s.replaceAll("~", "~0").replaceAll("/", "~1");

const paths = {};
for (const file of [...new Set(modules.map((m) => m.file))]) {
  const doc = YAML.parse(readFileSync(join(specDir, file), "utf8"));
  for (const p of Object.keys(doc.paths ?? {})) {
    if (paths[p]) throw new Error(`Duplicate path ${p} (second definition in ${file})`);
    paths[p] = { $ref: `./${file}#/paths/${escapePointer(p)}` };
  }
}

const rootDoc = {
  openapi: "3.1.0",
  info: {
    title: "Dhathuru API",
    version: "1.0.0",
    description:
      "Door-to-door trip planning and booking for the Maldives. Errors are RFC 9457 problem+json. Money is integer minor units. Time is ISO 8601 with offset.",
  },
  servers: [
    {
      url: "http://localhost:5100/v1",
      description: "Local API (Prism mock serves the same path on :4010)",
    },
  ],
  tags: modules.map(({ tag, description }) => ({ name: tag, description })),
  security: [{ bearerAuth: [] }],
  paths,
  components: {
    securitySchemes: {
      bearerAuth: { type: "http", scheme: "bearer", bearerFormat: "JWT" },
    },
  },
};

const tmpRoot = join(specDir, ".root.yaml");
writeFileSync(tmpRoot, YAML.stringify(rootDoc, { lineWidth: 0 }));

const bundledYaml = join(check ? join(root, ".check") : outDir, "openapi.bundled.yaml");
mkdirSync(dirname(bundledYaml), { recursive: true });
try {
  execFileSync(
    "pnpm",
    [
      "exec",
      "redocly",
      "bundle",
      tmpRoot,
      "-o",
      bundledYaml,
      "--config",
      join(root, "redocly.yaml"),
    ],
    {
      cwd: root,
      stdio: "inherit",
    },
  );
  const doc = YAML.parse(readFileSync(bundledYaml, "utf8"));
  const bundledJson = JSON.stringify(doc, null, 2) + "\n";
  if (check) {
    const committed = (f) =>
      existsSync(join(outDir, f)) ? readFileSync(join(outDir, f), "utf8") : "";
    const same =
      committed("openapi.bundled.yaml") === readFileSync(bundledYaml, "utf8") &&
      committed("openapi.bundled.json") === bundledJson;
    if (!same) {
      console.error(
        "Contract drift: generated/openapi.bundled.* is stale. Run `pnpm contracts` and commit.",
      );
      process.exitCode = 1;
    } else {
      console.log("Contract bundle is up to date.");
    }
  } else {
    writeFileSync(join(outDir, "openapi.bundled.json"), bundledJson);
    console.log(
      `Bundled ${Object.keys(paths).length} paths -> generated/openapi.bundled.{yaml,json}`,
    );
  }
} finally {
  rmSync(tmpRoot, { force: true });
  if (check) rmSync(join(root, ".check"), { recursive: true, force: true });
}

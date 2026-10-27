#!/usr/bin/env node
// One command for the whole local stack.
//   pnpm dev          real stack: Postgres + Mailpit (docker), .NET API, tile server, web (API_MODE=real)
//   pnpm dev:mock     light stack: Prism mock API, tile server, web (API_MODE=mock); no docker, no .NET
// Ctrl+C stops everything this script started (and only that). Ports can be overridden with the
// usual env vars (PORT, API_PORT, TILES_PORT, MOCK_PORT). Set Database__Name to pick another database.
import { spawn, spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import net from "node:net";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), "..");
const mock = process.argv.includes("--mock");
const ports = {
  web: Number(process.env.PORT ?? 3000),
  api: Number(process.env.API_PORT ?? 5100),
  tiles: Number(process.env.TILES_PORT ?? 8081),
  mock: Number(process.env.MOCK_PORT ?? 4010),
};
const tilesFile = path.join(root, "infra/tiles/out/maldives.pmtiles");
const hasTiles = existsSync(tilesFile);

const colors = { web: 36, api: 35, tiles: 33, mock: 32, dev: 90 };
const children = [];
let stopping = false;

function log(name, text) {
  const tag = `\x1b[${colors[name] ?? 0}m${name.padEnd(5)}\x1b[0m`;
  for (const line of String(text).split(/\r?\n/))
    if (line.trim()) process.stdout.write(`${tag} ${line}\n`);
}

function fail(message) {
  log("dev", `\x1b[31m${message}\x1b[0m`);
  stop(1);
}

function portBusy(port) {
  return new Promise((resolve) => {
    const socket = net.connect({ port, host: "127.0.0.1" });
    socket.once("connect", () => (socket.destroy(), resolve(true)));
    socket.once("error", () => resolve(false));
  });
}

function run(cmd, args, options = {}) {
  const result = spawnSync(cmd, args, { cwd: root, stdio: "inherit", ...options });
  return result.status === 0;
}

function start(name, cmd, args, env = {}) {
  if (stopping) return null;
  const child = spawn(cmd, args, {
    cwd: root,
    env: { ...process.env, ...env },
    detached: true, // own process group so Ctrl+C can stop the whole tree
    stdio: ["ignore", "pipe", "pipe"],
  });
  child.stdout.on("data", (d) => log(name, d));
  child.stderr.on("data", (d) => log(name, d));
  child.on("exit", (code) => {
    if (!stopping) fail(`${name} exited unexpectedly (code ${code}). Stopping the stack.`);
  });
  children.push(child);
  return child;
}

function stop(code = 0) {
  if (stopping) return;
  stopping = true;
  for (const child of children) {
    try {
      process.kill(-child.pid, "SIGTERM");
    } catch {
      /* already gone */
    }
  }
  setTimeout(() => process.exit(code), 1500).unref();
}
process.on("SIGINT", () => stop(0));
process.on("SIGTERM", () => stop(0));

async function waitFor(url, label, timeoutMs = 120_000) {
  const until = Date.now() + timeoutMs;
  while (Date.now() < until && !stopping) {
    try {
      const res = await fetch(url);
      if (res.status < 500) return true;
    } catch {
      /* not up yet */
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  if (!stopping) fail(`${label} did not become ready at ${url}`);
  return false;
}

// ---- preflight ----------------------------------------------------------------------------
const needed = [
  ports.web,
  ...(mock ? [ports.mock] : [ports.api]),
  ...(hasTiles ? [ports.tiles] : []),
];
for (const port of needed) {
  if (await portBusy(port))
    fail(
      `Port ${port} is already in use. Stop the process using it (or set PORT/API_PORT/TILES_PORT).`,
    );
}
if (stopping) await new Promise(() => {});

if (!existsSync(path.join(root, "packages/contracts/generated/api.d.ts"))) {
  log("dev", "Generating contracts...");
  if (!run("pnpm", ["contracts"])) fail("pnpm contracts failed");
}

const webEnv = {
  PORT: String(ports.web),
  API_MODE: mock ? "mock" : "real",
  API_BASE_URL: `http://localhost:${ports.api}/v1`,
  API_MOCK_URL: `http://localhost:${ports.mock}`,
  ...(hasTiles && !process.env.NEXT_PUBLIC_TILES_URL
    ? { NEXT_PUBLIC_TILES_URL: `http://localhost:${ports.tiles}` }
    : {}),
};

if (!mock) {
  if (!existsSync(path.join(root, "infra/.env"))) {
    log("dev", "First run: creating local credentials (scripts/dev-setup.sh)...");
    if (!run("bash", ["scripts/dev-setup.sh"])) fail("dev-setup failed");
  }
  log("dev", "Starting Postgres + Mailpit (docker compose)...");
  if (!run("docker", ["compose", "-f", "infra/docker-compose.yml", "up", "-d", "--wait"])) {
    fail("docker compose failed. Is Docker running?");
  }
}

// ---- processes ----------------------------------------------------------------------------
if (hasTiles)
  start("tiles", "node", ["infra/tiles/serve.mjs"], { TILES_PORT: String(ports.tiles) });
else
  log(
    "dev",
    "No infra/tiles/out/maldives.pmtiles: the map shows without a basemap (see infra/tiles/README.md).",
  );

if (mock) {
  start("mock", "pnpm", ["--filter", "@dhathuru/contracts", "run", "mock"]);
  await waitFor(`http://127.0.0.1:${ports.mock}/health`, "Prism mock");
} else {
  // Catalog tables and data come from KANDU's idempotent importer (the host migrates identity/bookings/platform itself).
  const password = readFileSync(path.join(root, "infra/.env"), "utf8")
    .match(/^POSTGRES_PASSWORD=(.*)$/m)?.[1]
    ?.trim();
  const databaseName = process.env.Database__Name ?? "dhathuru_main";
  log("dev", `Importing catalog seed into ${databaseName}...`);
  const imported = run(
    "bash",
    ["scripts/api.sh", "datatools", "import-catalog", "--seed", "seed"],
    {
      env: { ...process.env, Database__Name: databaseName, Database__Password: password ?? "" },
    },
  );
  if (!imported) fail("catalog import failed (see output above)");
  if (stopping) await new Promise(() => {});
  start("api", "bash", ["scripts/api.sh", "watch", "--no-launch-profile"], {
    ASPNETCORE_URLS: `http://localhost:${ports.api}`,
    ASPNETCORE_ENVIRONMENT: "Development",
    Database__Name: databaseName,
  });
  await waitFor(`http://localhost:${ports.api}/v1/health`, ".NET API");
}

start("web", "pnpm", ["--filter", "web", "dev"], webEnv);
if (await waitFor(`http://localhost:${ports.web}/api/health`, "web")) {
  log(
    "dev",
    `\x1b[32mReady\x1b[0m  web http://localhost:${ports.web}  api ${mock ? `mock :${ports.mock}` : `:${ports.api}`}  mode ${webEnv.API_MODE}`,
  );
  if (!mock)
    log(
      "dev",
      "Mail UI http://localhost:8025 . Ctrl+C stops the stack (Postgres keeps running: pnpm infra:down).",
    );
}

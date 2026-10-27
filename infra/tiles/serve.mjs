// Dev static server for PMTiles + glyphs on port 8081 (override with TILES_PORT).
// Serves infra/tiles/out/*.pmtiles and infra/tiles/assets/** with CORS and HTTP Range support,
// which PMTiles requires. Production uses object storage with Range support instead.
import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import { createServer } from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.dirname(fileURLToPath(import.meta.url));
const port = Number(process.env.TILES_PORT ?? 8081);
const roots = [path.join(root, "out"), path.join(root, "assets")];
const types = {
  ".pmtiles": "application/octet-stream",
  ".pbf": "application/x-protobuf",
  ".json": "application/json",
};

async function resolveFile(urlPath) {
  const rel = path.normalize(decodeURIComponent(urlPath)).replace(/^([/\\])+/, "");
  for (const base of roots) {
    const file = path.join(base, rel);
    if (!file.startsWith(base + path.sep)) continue;
    const info = await stat(file).catch(() => null);
    if (info?.isFile()) return { file, size: info.size };
  }
  return null;
}

createServer(async (req, res) => {
  res.setHeader("Access-Control-Allow-Origin", "*");
  res.setHeader("Access-Control-Allow-Headers", "Range, If-Match");
  res.setHeader("Access-Control-Expose-Headers", "Content-Range, Content-Length, ETag");
  res.setHeader("Accept-Ranges", "bytes");
  if (req.method === "OPTIONS") return res.writeHead(204).end();
  if (req.method !== "GET" && req.method !== "HEAD") return res.writeHead(405).end();

  const found = await resolveFile(new URL(req.url ?? "/", "http://x").pathname);
  if (!found) return res.writeHead(404).end("not found");

  res.setHeader("Content-Type", types[path.extname(found.file)] ?? "application/octet-stream");
  res.setHeader("Cache-Control", "no-cache");
  const match = /^bytes=(\d*)-(\d*)$/.exec(req.headers.range ?? "");
  let start = 0;
  let end = found.size - 1;
  if (match) {
    if (match[1] === "" && match[2] !== "") start = Math.max(0, found.size - Number(match[2]));
    else {
      start = Number(match[1]);
      if (match[2] !== "") end = Math.min(Number(match[2]), end);
    }
    if (start > end || start >= found.size) {
      return res.writeHead(416, { "Content-Range": `bytes */${found.size}` }).end();
    }
    res.writeHead(206, {
      "Content-Range": `bytes ${start}-${end}/${found.size}`,
      "Content-Length": end - start + 1,
    });
  } else {
    res.writeHead(200, { "Content-Length": found.size });
  }
  if (req.method === "HEAD") return res.end();
  createReadStream(found.file, { start, end }).pipe(res);
}).listen(port, () => console.log(`tiles server on http://localhost:${port}`));

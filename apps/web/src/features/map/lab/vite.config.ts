import react from "@vitejs/plugin-react";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vite";

/** Standalone dev harness for the map feature (port 3107): `pnpm --filter web map:lab`. */
export default defineConfig({
  root: fileURLToPath(new URL(".", import.meta.url)),
  plugins: [react()],
  resolve: { alias: { "@": fileURLToPath(new URL("../../..", import.meta.url)) } },
  optimizeDeps: { exclude: ["maplibre-gl"] },
  server: { port: 3107, strictPort: true, host: "127.0.0.1" },
  define: {
    "process.env.NEXT_PUBLIC_TILES_URL": JSON.stringify(
      process.env.NEXT_PUBLIC_TILES_URL ?? "http://localhost:8081",
    ),
  },
});

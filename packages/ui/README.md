# @dhathuru/ui

Dhathuru design system: tokens, components, icons and illustrations (owner: LAGOON).

- Docs: `docs/design/DIRECTION.md` (intent) and `docs/design/COMPONENTS.md` (API).
- Source-only package, no build step. `apps/web` depends on it via `workspace:*` and Tailwind scans it through `@source`.
- Tokens: `src/styles/theme.css` (Tailwind v4 `@theme`).
- Live gallery: run the web app and open `/dev/ui` (`NEXT_PUBLIC_ENABLE_UI_GALLERY=1` to expose in production).
- `pnpm --filter @dhathuru/ui test` (Vitest), `typecheck`, `lint`.
- `pnpm --filter @dhathuru/ui e2e` runs Playwright visual baselines and axe against `/dev/ui` and `/` (starts the web dev server on port 3106 if it is not already running); `e2e:update` regenerates snapshots.
- No built-in English: every visible or announced string is a prop.

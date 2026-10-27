# Dhathuru motion language (TIDE)

Metaphor: the lagoon. Things **arrive on a rising tide** (quick start, soft settle) and **leave on the ebb** (gentle start, brisk finish). Motion explains change; if it explains nothing, remove it.

Source of truth: `packages/motion/src/tokens.ts` (`@dhathuru/motion`). CSS mirror: `apps/web/src/motion/motion.css`.

## Tokens
| Token | Value | Use |
|---|---|---|
| `duration.instant` | 0.12 s | press feedback, colour shifts |
| `duration.quick` | 0.20 s | hover, focus, exits |
| `duration.base` | 0.35 s | reveals, popovers, swaps (default) |
| `duration.slow` | 0.55 s | panels, number tickers, ripples |
| `duration.tide` | 0.90 s | route drawing, hero atmosphere only |
| `ease.tide` | cubic-bezier(.22,.8,.28,1) | default: entering things |
| `ease.ebb` | cubic-bezier(.5,0,.75,.3) | leaving things |
| `ease.swell` | cubic-bezier(.45,.05,.25,1) | symmetric moves (pill, path drawing) |
| `spring.snappy` | k520 d40 | controls, pills, buttons |
| `spring.soft` | k260 d30 | cards, dialogs, toasts, shared elements |
| `spring.drift` | k120 d20 | magnetic / parallax follow |
| `stagger.step` | 50 ms, max 8 items | list entrances |
| `distance` | 6 / 12 / 24 px | nudge / rise / slide |
| `scale` | press .97, hover 1.015 | buttons, cards |

## Rules
1. **Animate transform and opacity only.** No width/height/top/left/box-shadow animation. Shadows change by cross-fading a pseudo-element's opacity.
2. **Nothing blocks input.** A user may never wait more than 0.6 s behind an animation (`MAX_BLOCKING_SECONDS`); view transitions set `pointer-events: none` on the overlay.
3. **Exits are shorter than entrances** (about 0.4x).
4. **Stagger at most 8 siblings**; the rest enter together. Never stagger results the user is scanning for the second time (re-sorting, filtering): swap them with `PresenceSwap`.
5. **Animate when** state changes meaning (loading to result, step to step), something is created or confirmed (toast, success bloom), the user is oriented in space (route direction, shared element, route being drawn), or a hold is running out (countdown pulse).
6. **Do not animate** on every scroll, on text that is being read, on form validation errors (show them instantly, announced), on anything repeating more than 3 times, or on data tables above 20 rows.
7. **No flashing**: nothing changes luminance more than 3 times per second; the pulse is 1 Hz and 45% opacity depth.
8. Shared things use one name: `layoutId`/`view-transition-name` strings are kebab-case and prefixed by feature (`route-<tripOptionId>`, `pill-<group>`).

## Reduced motion (`prefers-reduced-motion: reduce`)
Every primitive renders its **final state immediately** (no travel, no scale, no looping). Equivalents:
| Normal | Reduced |
|---|---|
| Reveal / Stagger rise | content simply present |
| Page transition slide + rise | 120 ms crossfade only |
| Sliding pill | pill jumps, 0 ms |
| NumberTicker count | final number at once |
| DrawPath | complete line |
| Ripple, Magnetic, Parallax, Lift | off |
| Skeleton shimmer | static tint |
| Countdown pulse | static, urgency shown by colour + text |
| Atmosphere canvas | static gradient |
`MotionProvider` also sets Motion's `reducedMotion="user"` so any library-level animation in other agents' code is covered.

## Catalogue
**Package `@dhathuru/motion`**: `Reveal`, `Stagger`, `PresenceSwap`, `NumberTicker`, `Magnetic`, `Parallax`, `DrawPath`, `Pressable`, `Lift`, `Ripple`, `Bloom`, `SlidingPill`/`PillGroup`, `Skeleton` (+ `dhathuru-pulse` class), `SharedLayout`, `useScrollMap`, `usePageProgress`, `useCountdown`, `useCopyToClipboard`, `usePrefersReducedMotion`, tokens.

**App layer `apps/web/src/motion`**: `MotionProvider` (mount once), `PageTransition`, `Atmosphere`, command palette, shortcut registry + help, toasts (`showToast`), recents (`recordRecent`).

## Recipes
- **Result cards**: `<Stagger>` the first render of a result list; re-sorts use `PresenceSwap` keyed by sort id.
- **Segmented control / tabs**: `<PillGroup>`; inside each item `<SlidingPill group="mode" active={selected} className="rounded-full bg-lagoon" />`; item is `position: relative`, label has `z-index` above the pill.
- **Route on the map**: `<DrawPath d={...} progress={scrubber} />`; the scrubber drives `progress` (0..1) so the timeline and route stay in sync.
- **Booking hold**: `const { label, urgent, expired } = useCountdown(hold.expiresAt)`; render `label` in a `<time>` and add the `dhathuru-pulse` class when `urgent`. Announce expiry with a toast (`hold.expired`).
- **Copy ticket code**: `useCopyToClipboard()`, show `<Bloom show={copied}>` and a toast on failure.
- **Money changing** (price after a filter): `<NumberTicker value={minor} format={(n) => intl.format(n / 100)} />`.
- **Directional navigation**: forward links `transitionTypes={[NAV_FORWARD]}`, back links `[NAV_BACK]`; the pages sit under `app/**/template.tsx` that renders `<PageTransition>`.

## Focus and accessibility
- Dialogs (palette/help) use cmdk/Radix: focus is trapped, moves to the search field on open, returns to the opener on Escape/close.
- Page transitions never move focus or scroll; Next handles scroll restoration. After a route change the destination page owns focus (skip link target / `h1`).
- Toasts are a polite live region and are never focus-stealing; every toast has a dismiss button and actions are real buttons.
- Animated numbers expose only the final value to assistive tech.
- Focus ring: 2 px lagoon outline, 2 px offset, on every interactive element (`motion.css`).

## Performance budget
60 fps on a mid-range laptop, INP < 200 ms. Atmosphere: half-resolution canvas, 30 fps, off-screen and hidden-tab pausing, lazy chunk, skipped on save-data, < 4 cores, < 4 GB memory.

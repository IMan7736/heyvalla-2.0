# Dhathuru visual direction (LAGOON, v1)

Tokens live in `packages/ui/src/styles/theme.css` (Tailwind v4 `@theme`). This document is the intent; the CSS is the truth.

## 1. Concept: "Salt & Lagoon"

An editorial travel product, not a SaaS dashboard. Think the printed timetable of a very well run ferry line crossed with a boarding pass: warm paper (sand), confident serif headlines, crisp hairlines, a few precise bursts of colour. The Maldives is seen the way a Maldivian sees it: a chain of low islands, one long horizon, light moving on shallow water, the dhoni, the jetty at dusk. Not honeymoon-resort clichés (no overwater-villa stock look, no palm silhouettes, no hibiscus, no sunset gradients).

Three qualities, in order: **calm** (nothing shouts, space is generous), **certain** (numbers, times and risk are legible at a glance), **local** (island-scale details, the horizon line, the dhoni, the water).

Signature motifs (reuse them, they make the product recognisable):
- **The horizon line:** a single hairline with a faint teal wash beneath it. Section dividers, hero, footer, empty states.
- **Ripple rings:** concentric thin circles from a point (a place, a stop). Map pins, success moment, loading.
- **Boarding-pass notch:** semicircle cut-outs and a perforated dashed rule on result and ticket cards.
- **Coral dot:** the one coral accent per surface, used like a highlighter (a label dot, a "now" marker, an underline). Never text.

## 2. Colour

Fixed brand base (Aiman): lagoon teal `#087F8C`, warm sand `#FAF7F0`, coconut white `#FFFFFF`, deep ocean `#12343B`, sunset coral `#F28C78`. Light theme only.

Derived scales (all in the theme file): `lagoon-50..800`, `sand-50..400`, `ocean-500..950`, `coral-50..700`. Semantic: `success #1F7A4D`, `caution #8A5206` (soft bg `#FDF0D5`), `danger #B3342B`, with `*-soft` backgrounds. Prefer role tokens in components: `canvas`, `surface`, `surface-sunken`, `ink`, `ink-muted`, `action`, `link`, `line`.

Measured contrast (WCAG ratio):
| Pair | Ratio | Rule |
|---|---|---|
| ocean `#12343B` on sand / white | 12.4 / 13.3 | Body and headings. |
| ocean-600 `#4A6A71` on sand / white | 5.5 / 5.9 | `ink-muted`: secondary text, placeholders. Never lighter. |
| white on lagoon `#087F8C` | 4.75 | Buttons (AA for normal text). |
| lagoon on sand `#087F8C` | 4.44 | FAILS for small text. Large text (24px+, or 19px bold) and icons only. |
| lagoon-600 `#066B76` on sand | 5.8 | `link`: all small teal text. |
| lagoon-700 `#05575F` on lagoon-50 | 7.4 | Teal badges and selected states. |
| coral `#F28C78` on sand | 2.2 | FAILS. Accent shapes only; never text, never the only signal. |
| ocean on coral | 5.6 | Text on a coral fill is allowed (ocean text). |
| coral-700 `#B5472F` on sand / coral-100 | 5.0 / 4.6 | Coral-family text and icons when needed. |
| success / caution / danger on own soft bg | 4.6 / 5.7 / 5.0 | Status text. |

Status is never colour alone: always an icon or word as well.

## 3. Typography

- **Display: Fraunces** (variable, optical size + SOFT axis, SOFT 60). A warm, slightly rounded old-style serif with character. Used for hero, page titles, big numerals on boarding-pass cards (arrival time), and pull quotes. Class `font-display`.
- **UI/text: Figtree** (variable). Geometric, friendly, excellent at small sizes, tabular figures for times and prices (`.tabular`).
- **Thaana, later:** `--font-thaana` is reserved (Noto Sans Thaana); `:lang(dv)` already swaps the face and leading. All spacing uses logical properties, so RTL flips cleanly.
- Both faces load through `next/font` in `apps/web/src/shell/fonts.ts` (exposed as `--font-display-face`, `--font-ui-face`), so there is no layout shift.

Scale (px at 16 root): caption 12/18, small 14/22, body 16/26, lead 20/30, title 24/32, headline 32/40, display 48/52, hero 76/76. Weights: UI 400/500/600, display 400-600. Display tracking -0.012em. Never more than two weights in one component. Sentence case everywhere; no all-caps except the 11px tracking-wide micro-labels on tickets.

## 4. Space, layout, radii, elevation

- 4px base grid, steps 4 / 8 / 12 / 16 / 20 / 24 / 32 / 40 / 56 / 80 / 120. Components pad on 12/16/24. Page gutters 48px at 1440, 32px at 1280. Max page width 1280 (`--container-page`). The design target is 1440, minimum 1280, no horizontal scroll.
- Always logical properties (`ps-`, `pe-`, `ms-`, `me-`, `start-`, `end-`, `inline-size`, `border-s`). Icons that imply direction (chevron, arrow) use `rtl:-scale-x-100`.
- Radii: xs 6 (tags), sm 10 (inputs, small buttons), md 14 (buttons, popovers), lg 20 (cards), xl 28 (hero panels), 2xl 36 (feature surfaces), full (pills, avatars).
- Elevation (ocean-tinted, two-layer shadows): `hairline` (1px ring), `rest` (cards), `raised` (hover, sticky bars), `float` (popovers), `overlay` (dialogs). Depth is layered with hairlines first, shadow second. Glass (`backdrop-blur` + 70% white + hairline) only on the sticky nav and the hero search panel.
- Focus: 2px lagoon ring, 2px offset (white on dark surfaces). Always visible; never removed.

## 5. Iconography

Original set in `packages/ui/src/icons`. 24px grid, 1.75px stroke, round caps and joins, 2px live-area padding, no fills except small detail dots. Two families:
- **Transport (9):** ferry, speedboat, seaplane, plane, bus, taxi, walk, jetty, airport. Each is drawn from a side or three-quarter silhouette with the same waterline/ground baseline at y=19 so they align in a row. Plus `resort_transfer` (boat with parasol).
- **Interface (~30):** chevrons, arrows, check, close, search, calendar, clock, user, users, swap, pin, alert, info, ticket, qr, menu, bell, globe, shield, wallet, route, anchor, wave, refresh, download, share, help, lock, mail.
Mode colours are NOT a rainbow: modes are distinguished by icon, label and a subtle tonal step of teal/ocean (sea modes lagoon, air modes ocean, land modes sand-deep ink). Coral is never a mode colour.

## 6. Illustration and imagery

Flat, layered paper-cut style: 3-5 tonal layers per scene (lagoon-100 to lagoon-800, sand, a single coral sun/dot), soft grain-free gradients, hairline outlines only on the dhoni. Subjects: low islands with a thin sand rim, the dhoni (curved raised bow, single mast, cabin), jetty with a lamp, reef shapes, seaplane on water, horizon. All original SVG in `packages/ui/src/illustrations`. No photographs in the product chrome (photos can be added later by Aiman; reserve a 16:9 slot and a tonal fallback). No people.

## 7. Voice and tone

Calm, plain, specific, quietly warm; a knowledgeable island friend, not a marketer.
- Numbers first, adjectives never: "You arrive 16:40, 20 minutes before you need to." not "Amazing journey!"
- Say the risk honestly: "Tight connection (12 min). We would not book this one." Safe beats fast, and we say so.
- Errors say what happened and what to do next in one breath. No blame, no exclamation marks, no jargon.
- Say "estimated" whenever it is (taxi fare, sea route). Dates "Tue 27 Oct", times 24-hour "16:40", Maldives time unless stated. Dhivehi place names are spelled the way the operator spells them.
- Microcopy lives in next-intl files; this section is the editorial guide for whoever writes them.

## 8. Motion

CSS-only inside the design system (TIDE adds richer choreography in `packages/motion`). Transform and opacity only. Durations 120 / 220 / 420ms; easing `--ease-calm`; `--ease-swell` for arrivals and the confirmation moment. Ambient motion (water drift, caustics, dhoni bob) is slow (12-30s loops) and stops under `prefers-reduced-motion`. Entering overlays fade+rise 8px; nothing bounces except the confirmation ripple.

## 9. Signature components

1. **Lagoon hero:** layered gradient water, two slow-drifting caustic layers (SVG patterns moved by transform), horizon line, original dhoni with a gentle bob, islands, a pale sun; search panel in glass over it.
2. **Journey ribbon:** one continuous line, node per interchange, mode icon chips on segments sized by duration, buffers drawn as labelled gaps (colour + icon + text for comfortable/tight).
3. **Boarding-pass result card:** left stub (labels, mode icons), centre (depart to arrive, large display arrival), right stub (price, risk meter) separated by a perforated notch.
4. **Buffer meter / risk gauge:** accessible meters with `role="meter"` and text equivalents.
5. **Ticket wallet card:** QR on white, teal-to-ocean gradient strip, diagonal sheen on hover/focus.
6. **Booking confirmed:** ripple rings expand from a check, the route draws itself on a mini horizon; settles in under 1.2s; static under reduced motion.
7. **Empty / error / offline states:** a small illustration each (empty lagoon, reef snag, drifting dhoni), one sentence, one action.

## 10. Accessibility commitments

WCAG 2.2 AA: contrast table above, 24px minimum targets (44px for primary actions), keyboard for everything, Radix semantics for dialogs/menus/tabs/select, `aria-live` toasts, `role="meter"` / `role="progressbar"` data-viz with text equivalents, reduced-motion respected, no information by colour alone, forms with programmatic labels, hints and errors (`aria-describedby`, `aria-invalid`).

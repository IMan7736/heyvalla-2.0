import type { ReactNode } from "react";

/**
 * Original Dhathuru icon set. 24px grid, 1.75px stroke, round caps and joins, 2px padding.
 * Transport icons share a baseline at y=19 so they align in a row.
 */
export const transportIconNames = [
  "ferry",
  "speedboat",
  "seaplane",
  "plane",
  "bus",
  "taxi",
  "walk",
  "jetty",
  "airport",
  "resort-transfer",
] as const;

export const interfaceIconNames = [
  "chevron-down",
  "chevron-up",
  "chevron-left",
  "chevron-right",
  "arrow-right",
  "arrow-left",
  "check",
  "check-circle",
  "x",
  "plus",
  "minus",
  "search",
  "calendar",
  "clock",
  "user",
  "users",
  "swap",
  "pin",
  "alert",
  "info",
  "ticket",
  "qr",
  "menu",
  "bell",
  "globe",
  "shield",
  "wallet",
  "route",
  "anchor",
  "wave",
  "refresh",
  "download",
  "share",
  "help",
  "lock",
  "mail",
  "wifi-off",
  "compass",
  "luggage",
  "card",
  "logout",
  "settings",
  "trash",
  "edit",
] as const;

export type TransportIconName = (typeof transportIconNames)[number];
export type InterfaceIconName = (typeof interfaceIconNames)[number];
export type IconName = TransportIconName | InterfaceIconName;

export const iconPaths: Record<IconName, ReactNode> = {
  // ---------- transport ----------
  ferry: (
    <>
      <path d="M3 14.5h18l-2.4 4.5H5.4z" />
      <path d="M5.5 14.5V10.5h13v4" />
      <path d="M9 10.5V7.5h6v3" />
      <path d="M12 7.5v-3" />
      <path d="M8.5 12.6h.01M12 12.6h.01M15.5 12.6h.01" />
    </>
  ),
  speedboat: (
    <>
      <path d="M2.5 13.5h13.2l5.8-3.2-1.4 6.2a2.5 2.5 0 0 1-2.4 2H6.3a2.5 2.5 0 0 1-2.4-1.8z" />
      <path d="M9 13.5l2.2-4.2h3.3l2.2 4.2" />
    </>
  ),
  seaplane: (
    <>
      <path d="M3 13.5l3.2-.9 4.6-5.1h2.3l-2 5 6.2-1.7c1.7-.45 3.4.8 3.4 1.5 0 .7-1.7 1.9-3.4 1.4L11.1 14l2 4.6h-2.3L6.2 14.8z" />
      <path d="M3.5 21h4M10 21h4M16.5 21h4" />
    </>
  ),
  plane: (
    <>
      <path
        d="M2.8 13.2l7.2-2.3 3.3-6.2h2.3l-1.5 6.4 5.8-1.5c1.3-.3 2.2.4 2.2 1s-.9 1.3-2.2 1l-5.8-1.5 1.5 6.4h-2.3L10 14.1l-7.2-2.3z"
        transform="translate(0 1.2) scale(.97)"
      />
    </>
  ),
  bus: (
    <>
      <rect x="4" y="4.5" width="16" height="12.5" rx="3" />
      <path d="M4 11h16" />
      <path d="M7.5 17v2.3M16.5 17v2.3" />
      <path d="M7.8 14h.01M16.2 14h.01" />
    </>
  ),
  taxi: (
    <>
      <path d="M3.5 15.5v-2.3a1.5 1.5 0 0 1 .6-1.2l1.7-1.3 1.3-3A1.8 1.8 0 0 1 8.8 6.6h6.4a1.8 1.8 0 0 1 1.7 1.1l1.3 3 1.7 1.3a1.5 1.5 0 0 1 .6 1.2v2.3z" />
      <path d="M3.5 15.5v2.3h3v-2.3M17.5 15.5v2.3h3v-2.3" />
      <path d="M9.8 3.6h4.4" />
      <path d="M7.2 12h.01M16.8 12h.01" />
      <path d="M6.5 10.5h11" />
    </>
  ),
  walk: (
    <>
      <circle cx="13" cy="4.6" r="1.7" />
      <path d="M10.2 20l1.5-5.2-2.2-2.2.7-4.1 3.7-.7 2.2 3.3 2.4.6" />
      <path d="M11.7 14.8l3.2 1.5.9 3.7" />
      <path d="M9.4 12.6L7 13.4" />
    </>
  ),
  jetty: (
    <>
      <path d="M2.5 11.5h19" />
      <path d="M5 11.5V19M10 11.5V19M15 11.5V19M20 11.5V19" />
      <path d="M7.5 11.5V6a1.5 1.5 0 0 1 3 0v5.5" />
    </>
  ),
  airport: (
    <>
      <path d="M3 19h18" />
      <path d="M5.5 19v-7.5h5V19M10.5 19V9.5h8V19" />
      <path d="M14.5 9.5V6M12.5 6h4" />
      <path d="M7.7 14.2h.01M13 12.5h.01M16 12.5h.01M13 15.5h.01M16 15.5h.01" />
    </>
  ),
  "resort-transfer": (
    <>
      <path d="M3 15.5h18l-2.2 3.8H5.2z" />
      <path d="M12 15.5V6" />
      <path d="M5.5 10.2A6.5 6.5 0 0 1 12 6a6.5 6.5 0 0 1 6.5 4.2z" />
    </>
  ),
  // ---------- interface ----------
  "chevron-down": <path d="M6 9.5l6 6 6-6" />,
  "chevron-up": <path d="M6 14.5l6-6 6 6" />,
  "chevron-left": <path d="M14.5 6l-6 6 6 6" />,
  "chevron-right": <path d="M9.5 6l6 6-6 6" />,
  "arrow-right": <path d="M4.5 12h15M13.5 6l6 6-6 6" />,
  "arrow-left": <path d="M19.5 12h-15M10.5 6l-6 6 6 6" />,
  check: <path d="M5 12.5l4.5 4.5L19 7.5" />,
  "check-circle": (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M7.8 12.4l3 3 5.4-6" />
    </>
  ),
  x: <path d="M6 6l12 12M18 6L6 18" />,
  plus: <path d="M12 5v14M5 12h14" />,
  minus: <path d="M5 12h14" />,
  search: (
    <>
      <circle cx="11" cy="11" r="6.5" />
      <path d="M16 16l4.5 4.5" />
    </>
  ),
  calendar: (
    <>
      <rect x="3.5" y="5" width="17" height="15.5" rx="3" />
      <path d="M3.5 10h17M8 3v4M16 3v4" />
    </>
  ),
  clock: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v5.2l3.2 2" />
    </>
  ),
  user: (
    <>
      <circle cx="12" cy="8" r="3.8" />
      <path d="M4.5 20c.9-3.6 3.8-5.5 7.5-5.5s6.6 1.9 7.5 5.5" />
    </>
  ),
  users: (
    <>
      <circle cx="9" cy="8.5" r="3.3" />
      <path d="M2.8 19.5c.8-3.2 3.1-4.8 6.2-4.8s5.4 1.6 6.2 4.8" />
      <path d="M15.5 5.5a3.3 3.3 0 0 1 0 6M18 15c1.8.6 2.8 2 3.2 4.5" />
    </>
  ),
  swap: <path d="M4 8h15l-3.5-3.5M20 16H5l3.5 3.5" />,
  pin: (
    <>
      <path d="M12 21s-6.5-5.6-6.5-11a6.5 6.5 0 0 1 13 0c0 5.4-6.5 11-6.5 11z" />
      <circle cx="12" cy="10" r="2.4" />
    </>
  ),
  alert: (
    <>
      <path d="M12 3.8l9 15.7H3z" />
      <path d="M12 10v4.4M12 17.2h.01" />
    </>
  ),
  info: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11v5.2M12 7.8h.01" />
    </>
  ),
  ticket: (
    <>
      <path d="M3.5 8.5a2 2 0 0 0 0 7V18a1 1 0 0 0 1 1h15a1 1 0 0 0 1-1v-2.5a2 2 0 0 1 0-7V6a1 1 0 0 0-1-1h-15a1 1 0 0 0-1 1z" />
      <path d="M14.5 5.5v2M14.5 11v2M14.5 16.5v2" />
    </>
  ),
  qr: (
    <>
      <rect x="4" y="4" width="6" height="6" rx="1" />
      <rect x="14" y="4" width="6" height="6" rx="1" />
      <rect x="4" y="14" width="6" height="6" rx="1" />
      <path d="M14 14h2.5v2.5M20 14v.01M14 20h.01M17.5 20H20v-2.5" />
    </>
  ),
  menu: <path d="M4 7h16M4 12h16M4 17h16" />,
  bell: (
    <>
      <path d="M6 16.5V11a6 6 0 0 1 12 0v5.5l1.5 2h-15z" />
      <path d="M10 21h4" />
    </>
  ),
  globe: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M3 12h18M12 3c2.6 2.5 3.9 5.5 3.9 9S14.6 18.5 12 21c-2.6-2.5-3.9-5.5-3.9-9S9.4 5.5 12 3z" />
    </>
  ),
  shield: (
    <>
      <path d="M12 3l7.5 2.8v5.4c0 4.6-3.2 8.2-7.5 9.8-4.3-1.6-7.5-5.2-7.5-9.8V5.8z" />
      <path d="M8.8 12l2.2 2.2 4.2-4.6" />
    </>
  ),
  wallet: (
    <>
      <path d="M4 7.5a2.5 2.5 0 0 1 2.5-2.5H18v3" />
      <rect x="3.5" y="7.5" width="17" height="12" rx="3" />
      <path d="M16.5 13.5h.01" />
    </>
  ),
  route: (
    <>
      <circle cx="6" cy="18" r="2.2" />
      <circle cx="18" cy="6" r="2.2" />
      <path d="M8.2 18H15a3 3 0 0 0 0-6H9a3 3 0 0 1 0-6h6.8" />
    </>
  ),
  anchor: (
    <>
      <circle cx="12" cy="5.5" r="2" />
      <path d="M12 7.5V21M7.5 11h9M4 14.5c.4 3.2 3.6 6.5 8 6.5s7.6-3.3 8-6.5" />
    </>
  ),
  wave: (
    <path d="M2.5 9c2 0 2-1.5 4-1.5S8.5 9 10.5 9s2-1.5 4-1.5S16.5 9 18.5 9s2-1.5 3-1.5M2.5 15c2 0 2-1.5 4-1.5s2 1.5 4 1.5 2-1.5 4-1.5 2 1.5 4 1.5 2-1.5 3-1.5" />
  ),
  refresh: <path d="M19.5 12a7.5 7.5 0 1 1-2.4-5.5M19.5 4.5v4h-4" />,
  download: <path d="M12 4v11M7 11l5 5 5-5M5 20h14" />,
  share: (
    <>
      <path d="M12 15V4M8 7.5L12 3.5l4 4" />
      <path d="M6 11.5v7a1.5 1.5 0 0 0 1.5 1.5h9a1.5 1.5 0 0 0 1.5-1.5v-7" />
    </>
  ),
  help: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M9.6 9.6a2.5 2.5 0 1 1 3.4 2.3c-.7.3-1 .9-1 1.6M12 16.8h.01" />
    </>
  ),
  lock: (
    <>
      <rect x="5" y="10.5" width="14" height="10" rx="2.5" />
      <path d="M8 10.5V8a4 4 0 0 1 8 0v2.5M12 15v1.8" />
    </>
  ),
  mail: (
    <>
      <rect x="3.5" y="5.5" width="17" height="13" rx="3" />
      <path d="M4.5 8l7.5 5.5L19.5 8" />
    </>
  ),
  "wifi-off": (
    <>
      <path d="M3 3l18 18M8.5 16.2a5 5 0 0 1 7 0M5.2 12.4a9.5 9.5 0 0 1 3-1.9M18.8 12.4a9.5 9.5 0 0 0-5.3-2.8M2.5 8.8a14 14 0 0 1 4.3-2.6M21.5 8.8A14 14 0 0 0 12 5" />
      <path d="M12 19.5h.01" />
    </>
  ),
  compass: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M15.8 8.2l-2 5.6-5.6 2 2-5.6z" />
    </>
  ),
  luggage: (
    <>
      <rect x="5.5" y="7" width="13" height="13" rx="3" />
      <path d="M9.5 7V4.5h5V7M9.5 11v5M14.5 11v5" />
    </>
  ),
  card: (
    <>
      <rect x="3" y="5.5" width="18" height="13" rx="3" />
      <path d="M3 10h18M7 15h3" />
    </>
  ),
  logout: (
    <path d="M10 4.5H6.5A1.5 1.5 0 0 0 5 6v12a1.5 1.5 0 0 0 1.5 1.5H10M15 8l4 4-4 4M19 12H9.5" />
  ),
  settings: (
    <>
      <circle cx="12" cy="12" r="3" />
      <path d="M12 3.5v2.2M12 18.3v2.2M20.5 12h-2.2M5.7 12H3.5M18 6l-1.5 1.5M7.5 16.5L6 18M18 18l-1.5-1.5M7.5 7.5L6 6" />
    </>
  ),
  trash: (
    <path d="M4.5 7h15M9.5 7V4.5h5V7M6.5 7l.8 12.2a1.5 1.5 0 0 0 1.5 1.3h6.4a1.5 1.5 0 0 0 1.5-1.3L17.5 7M10 11v6M14 11v6" />
  ),
  edit: (
    <path d="M14.5 5.5l4 4M4.5 19.5l.8-4L16.2 4.6a1.4 1.4 0 0 1 2 0l1.2 1.2a1.4 1.4 0 0 1 0 2L8.5 18.7z" />
  ),
};

export function isIconName(name: string): name is IconName {
  return name in iconPaths;
}

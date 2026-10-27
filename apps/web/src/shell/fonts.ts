import { Figtree, Fraunces } from "next/font/google";

/** Display serif: warm, soft, optical-size aware. Used for titles, hero and big numerals. */
export const displayFont = Fraunces({
  subsets: ["latin"],
  axes: ["opsz", "SOFT"],
  variable: "--font-display-face",
  display: "swap",
});

/** UI sans: friendly geometric face with tabular figures. */
export const uiFont = Figtree({
  subsets: ["latin"],
  variable: "--font-ui-face",
  display: "swap",
});

export const fontClassName = `${displayFont.variable} ${uiFont.variable}`;

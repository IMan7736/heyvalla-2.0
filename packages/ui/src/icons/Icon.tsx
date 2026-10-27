import type { SVGProps } from "react";
import { iconPaths, type IconName } from "./icons";

export type IconProps = Omit<SVGProps<SVGSVGElement>, "children" | "name"> & {
  name: IconName;
  /** Pixel size (square). Defaults to 20. */
  size?: number;
  /** Accessible name. Omit for decorative icons (the icon is then hidden from assistive tech). */
  label?: string;
  /** Mirror in right-to-left layouts (chevrons, arrows). */
  directional?: boolean;
};

export function Icon({ name, size = 20, label, directional, className, ...rest }: IconProps) {
  const a11y = label
    ? ({ role: "img", "aria-label": label } as const)
    : ({ "aria-hidden": true, focusable: false } as const);
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      width={size}
      height={size}
      fill="none"
      stroke="currentColor"
      strokeWidth={1.75}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={[directional ? "rtl:-scale-x-100" : "", "shrink-0", className ?? ""]
        .filter(Boolean)
        .join(" ")}
      {...a11y}
      {...rest}
    >
      {iconPaths[name]}
    </svg>
  );
}

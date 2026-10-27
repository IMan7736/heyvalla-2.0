import { Icon } from "../icons/Icon";
import { cn } from "../lib/cn";
import { familyChip, modeFamily, modeIcon, type TransportMode } from "./modes";

/** Mode glyph in a tonal rounded chip. `label` makes it an image for assistive tech. */
export function ModeChip({
  mode,
  size = 36,
  label,
  className,
}: {
  mode: TransportMode;
  size?: number;
  label?: string;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-grid shrink-0 place-items-center rounded-full",
        familyChip[modeFamily[mode]],
        className,
      )}
      style={{ width: size, height: size }}
    >
      <Icon name={modeIcon[mode]} size={Math.round(size * 0.58)} label={label} />
    </span>
  );
}

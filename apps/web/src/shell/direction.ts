/** Languages written right to left. Dhivehi (Thaana) is the one the product plans for. */
const rtlLanguages = new Set(["dv", "ar", "he", "fa", "ur"]);

/** Text direction for the `<html dir>` attribute, derived from the active locale. */
export function directionOf(locale: string): "ltr" | "rtl" {
  const language = locale.toLowerCase().split("-")[0] ?? "";
  return rtlLanguages.has(language) ? "rtl" : "ltr";
}

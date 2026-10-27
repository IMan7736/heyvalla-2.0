import { readdir, readFile } from "node:fs/promises";
import path from "node:path";
import { getRequestConfig } from "next-intl/server";

export const defaultLocale = "en";
export const locales = [defaultLocale] as const;

const messagesRoot = path.join(process.cwd(), "messages");

/**
 * One JSON file per feature: messages/<locale>/<feature>.json is exposed under the
 * `<feature>` namespace (useTranslations("planner") reads messages/en/planner.json).
 */
export async function loadMessages(locale: string): Promise<Record<string, unknown>> {
  const dir = path.join(messagesRoot, locale);
  const files = (await readdir(dir)).filter((f) => f.endsWith(".json")).sort();
  const entries = await Promise.all(
    files.map(
      async (file) =>
        [
          path.basename(file, ".json"),
          JSON.parse(await readFile(path.join(dir, file), "utf8")),
        ] as const,
    ),
  );
  return Object.fromEntries(entries);
}

export default getRequestConfig(async () => {
  const locale = defaultLocale;
  return { locale, timeZone: "Indian/Maldives", messages: await loadMessages(locale) };
});

import { Accordion } from "@dhathuru/ui";
import { getTranslations } from "next-intl/server";

export async function generateMetadata() {
  const t = await getTranslations("help");
  return { title: t("title") };
}

const questions = ["buffers", "risk", "estimated", "late", "time"] as const;

export default async function HelpPage() {
  const t = await getTranslations("help");
  return (
    <main id="main" className="mx-auto max-w-[var(--container-prose)] px-8 py-20">
      <h1 className="font-display text-display text-ink">{t("title")}</h1>
      <p className="mt-4 text-lead text-ink-muted">{t("lead")}</p>
      <Accordion
        headingLevel={2}
        className="mt-12 border-y border-line"
        items={questions.map((q) => ({
          id: q,
          title: t(`faq.${q}.q`),
          content: t(`faq.${q}.a`),
        }))}
      />
    </main>
  );
}

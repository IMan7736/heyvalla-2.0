import { Alert } from "@dhathuru/ui";
import { getTranslations } from "next-intl/server";

export async function generateMetadata() {
  const t = await getTranslations("legal.privacy");
  return { title: t("title") };
}

const sections = ["collect", "use", "rights"] as const;

export default async function LegalPage() {
  const t = await getTranslations("legal");
  return (
    <main id="main" className="mx-auto max-w-[var(--container-prose)] px-8 py-20">
      <h1 className="font-display text-display text-ink">{t("privacy.title")}</h1>
      <Alert tone="caution" className="mt-6">
        {t("draftNotice")}
      </Alert>
      <div className="mt-10 flex flex-col gap-8">
        {sections.map((s) => (
          <section key={s}>
            <h2 className="font-display text-title text-ink">{t(`privacy.sections.${s}.title`)}</h2>
            <p className="mt-2 text-body text-ink-muted">{t(`privacy.sections.${s}.body`)}</p>
          </section>
        ))}
      </div>
    </main>
  );
}

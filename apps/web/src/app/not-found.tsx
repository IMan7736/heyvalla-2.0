import { Button, EmptyState } from "@dhathuru/ui";
import { getTranslations } from "next-intl/server";
import Link from "next/link";
import { PageFrame } from "@/shell/PageFrame";

export async function generateMetadata() {
  const t = await getTranslations("errors.notFound");
  return { title: t("title") };
}

export default async function NotFound() {
  const t = await getTranslations("errors.notFound");
  return (
    <PageFrame>
      <main id="main" className="mx-auto max-w-[var(--container-page)] px-8 py-24">
        <EmptyState
          headingLevel={1}
          title={t("title")}
          description={t("description")}
          action={
            <Button asChild size="lg">
              <Link href="/plan">{t("plan")}</Link>
            </Button>
          }
          secondaryAction={
            <Button asChild variant="ghost" size="lg">
              <Link href="/">{t("home")}</Link>
            </Button>
          }
        />
      </main>
    </PageFrame>
  );
}

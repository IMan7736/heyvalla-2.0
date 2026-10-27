import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { Hero } from "./_components/Hero";
import { ClosingCta, Honesty, HowItWorks, Modes, NeverMissARide } from "./_components/Sections";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("home");
  return { title: { absolute: t("metaTitle") }, description: t("metaDescription") };
}

export default function HomePage() {
  return (
    <main id="main">
      <Hero />
      <HowItWorks />
      <NeverMissARide />
      <Modes />
      <Honesty />
      <ClosingCta />
    </main>
  );
}

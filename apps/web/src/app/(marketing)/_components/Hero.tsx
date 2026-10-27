import { LagoonScene } from "@dhathuru/ui";
import { getTranslations } from "next-intl/server";
import { TripSearchForm } from "@/features/planner";

/** Landing hero: living lagoon behind the headline, search form framed in glass. */
export async function Hero() {
  const t = await getTranslations("home");
  return (
    <section aria-labelledby="hero-title">
      <LagoonScene className="min-h-[820px]">
        <div
          className="mx-auto flex max-w-[var(--container-page)] flex-col items-center px-8 pt-20 pb-28 text-center min-[90rem]:px-12"
        >
          <p className="mb-5 inline-flex items-center gap-2 rounded-full bg-white/70 px-4 py-1.5 text-small font-medium text-ocean-800 shadow-[var(--shadow-hairline)] backdrop-blur">
            <span aria-hidden="true" className="size-1.5 rounded-full bg-coral" />
            {t("eyebrow")}
          </p>
          <h1
            id="hero-title"
            className="font-display max-w-[14ch] text-hero text-ocean-950 text-balance"
          >
            {t("title")}
          </h1>
          <p className="mt-6 max-w-[40ch] text-lead text-ocean-800 text-balance">{t("lead")}</p>
          <div className="mt-12 w-full max-w-[920px] rounded-2xl bg-white/80 p-3 text-start shadow-[var(--shadow-hairline),var(--shadow-overlay)] backdrop-blur-xl">
            <TripSearchForm compact />
          </div>
        </div>
      </LagoonScene>
    </section>
  );
}

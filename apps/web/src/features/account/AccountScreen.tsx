"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState, type ReactNode } from "react";
import { useTranslations } from "next-intl";
import { Alert, Button, Field, Input, Select } from "@dhathuru/ui";
import { account } from "./api";
import { postJson } from "./post-json";

type User = { id: string; email: string; emailVerified: boolean; displayName?: string | null };
type Prefs = {
  language: string;
  riskTolerance: "cautious" | "balanced" | "relaxed";
  preferredModes: string[];
};
type Traveller = { id: string; fullName: string; passengerType: "adult" | "child" | "infant" };
type Session = { id: string; lastUsedAt: string; userAgent?: string | null; current: boolean };

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section
      className="flex flex-col gap-4 rounded-2xl bg-surface p-6 shadow-sm"
      aria-label={title}
    >
      <h2 className="font-display text-xl text-ink">{title}</h2>
      {children}
    </section>
  );
}

export function AccountScreen() {
  const t = useTranslations("account");
  const router = useRouter();
  const [state, setState] = useState<"loading" | "ready" | "failed">("loading");
  const [user, setUser] = useState<User | null>(null);
  const [prefs, setPrefs] = useState<Prefs | null>(null);
  const [travellers, setTravellers] = useState<Traveller[]>([]);
  const [sessions, setSessions] = useState<Session[]>([]);

  const errorText = (code: string) =>
    t.has(`errors.${code}`) ? t(`errors.${code}`) : t("errors.GENERIC");

  const load = useCallback(async () => {
    setState("loading");
    const [me, p, tr, se] = await Promise.all([
      account<User>(""),
      account<Prefs>("/preferences"),
      account<{ items: Traveller[] }>("/travellers"),
      account<{ items: Session[] }>("/sessions"),
    ]);
    if (!me.ok && me.problem.status === 401) return router.replace("/login?returnTo=/account");
    if (!me.ok || !p.ok || !tr.ok || !se.ok) return setState("failed");
    setUser(me.data);
    setPrefs(p.data);
    setTravellers(tr.data.items);
    setSessions(se.data.items);
    setState("ready");
  }, [router]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- initial data load
    void load();
  }, [load]);

  if (state === "loading")
    return (
      <main id="main" className="mx-auto max-w-2xl px-6 py-16" aria-busy="true">
        <p role="status">{t("manage.loading")}</p>
      </main>
    );
  if (state === "failed" || !user || !prefs) {
    return (
      <main id="main" className="mx-auto flex max-w-2xl flex-col gap-4 px-6 py-16">
        <Alert tone="danger">{t("manage.loadFailed")}</Alert>
        <Button onClick={() => void load()}>{t("manage.retry")}</Button>
      </main>
    );
  }

  return (
    <main id="main" className="mx-auto flex w-full max-w-2xl flex-col gap-6 px-6 py-12">
      <header className="flex items-center justify-between gap-4">
        <h1 className="font-display text-3xl text-ink">{t("manage.title")}</h1>
        <Button
          variant="secondary"
          onClick={async () => {
            await postJson("/api/auth/logout", {});
            router.replace("/login");
            router.refresh();
          }}
        >
          {t("manage.signOut")}
        </Button>
      </header>
      {!user.emailVerified ? <Alert tone="caution">{t("manage.unverified")}</Alert> : null}
      <ProfileSection user={user} onSaved={setUser} errorText={errorText} />
      <PreferencesSection prefs={prefs} onSaved={setPrefs} errorText={errorText} />
      <TravellersSection items={travellers} onChange={setTravellers} errorText={errorText} />
      <SecuritySection sessions={sessions} onReload={load} errorText={errorText} />
      <DeleteSection
        errorText={errorText}
        onDeleted={() => {
          router.replace("/");
          router.refresh();
        }}
      />
    </main>
  );
}

type WithError = { errorText: (code: string) => string };

function ProfileSection({
  user,
  onSaved,
  errorText,
}: { user: User; onSaved: (u: User) => void } & WithError) {
  const t = useTranslations("account.manage.profile");
  const f = useTranslations("account.fields");
  const [name, setName] = useState(user.displayName ?? "");
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null);
  return (
    <Section title={t("title")}>
      <form
        className="flex flex-col gap-4"
        onSubmit={async (e) => {
          e.preventDefault();
          const r = await account<User>("", { method: "PATCH", body: { displayName: name } });
          if (r.ok) {
            onSaved(r.data);
            setMsg({ ok: true, text: t("saved") });
          } else
            setMsg({ ok: false, text: errorText(r.problem.fields[0]?.code ?? r.problem.code) });
        }}
      >
        <Field label={t("email")}>
          <Input value={user.email} readOnly />
        </Field>
        <Field label={f("displayName")}>
          <Input
            value={name}
            maxLength={80}
            autoComplete="name"
            onChange={(e) => setName(e.target.value)}
          />
        </Field>
        <Button type="submit" className="self-start">
          {t("save")}
        </Button>
        {msg ? <p role={msg.ok ? "status" : "alert"}>{msg.text}</p> : null}
      </form>
    </Section>
  );
}

function PreferencesSection({
  prefs,
  onSaved,
  errorText,
}: { prefs: Prefs; onSaved: (p: Prefs) => void } & WithError) {
  const t = useTranslations("account.manage.preferences");
  const [risk, setRisk] = useState(prefs.riskTolerance);
  const [msg, setMsg] = useState<string | null>(null);
  return (
    <Section title={t("title")}>
      <form
        className="flex flex-col gap-4"
        onSubmit={async (e) => {
          e.preventDefault();
          const r = await account<Prefs>("/preferences", {
            method: "PATCH",
            body: { riskTolerance: risk },
          });
          if (r.ok) {
            onSaved(r.data);
            setMsg(t("save"));
          } else setMsg(errorText(r.problem.code));
        }}
      >
        <Field label={t("risk")}>
          <Select
            value={risk}
            placeholder={t("risk")}
            onValueChange={(v) => setRisk(v as Prefs["riskTolerance"])}
            options={[
              { value: "cautious", label: t("cautious") },
              { value: "balanced", label: t("balanced") },
              { value: "relaxed", label: t("relaxed") },
            ]}
          />
        </Field>
        <Button type="submit" className="self-start">
          {t("save")}
        </Button>
        {msg ? <p role="status">{msg}</p> : null}
      </form>
    </Section>
  );
}

function TravellersSection({
  items,
  onChange,
  errorText,
}: { items: Traveller[]; onChange: (t: Traveller[]) => void } & WithError) {
  const t = useTranslations("account.manage.travellers");
  const [name, setName] = useState("");
  const [type, setType] = useState<Traveller["passengerType"]>("adult");
  const [error, setError] = useState<string | null>(null);
  return (
    <Section title={t("title")}>
      {items.length === 0 ? (
        <p>{t("empty")}</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((tr) => (
            <li key={tr.id} className="flex items-center justify-between gap-3">
              <span>
                {tr.fullName} <span className="text-ink-muted">· {t(tr.passengerType)}</span>
              </span>
              <Button
                variant="ghost"
                size="sm"
                aria-label={t("removeLabel", { name: tr.fullName })}
                onClick={async () => {
                  const r = await account(`/travellers/${tr.id}`, { method: "DELETE" });
                  if (r.ok) onChange(items.filter((x) => x.id !== tr.id));
                  else setError(errorText(r.problem.code));
                }}
              >
                {t("remove")}
              </Button>
            </li>
          ))}
        </ul>
      )}
      <form
        className="flex flex-col gap-4"
        onSubmit={async (e) => {
          e.preventDefault();
          setError(null);
          const r = await account<Traveller>("/travellers", {
            method: "POST",
            body: { fullName: name, passengerType: type },
          });
          if (r.ok) {
            onChange([...items, r.data]);
            setName("");
          } else setError(errorText(r.problem.fields[0]?.code ?? r.problem.code));
        }}
      >
        <Field label={t("name")} required>
          <Input value={name} maxLength={120} onChange={(e) => setName(e.target.value)} />
        </Field>
        <Field label={t("type")}>
          <Select
            value={type}
            placeholder={t("type")}
            onValueChange={(v) => setType(v as Traveller["passengerType"])}
            options={[
              { value: "adult", label: t("adult") },
              { value: "child", label: t("child") },
              { value: "infant", label: t("infant") },
            ]}
          />
        </Field>
        <p className="text-sm text-ink-muted">{t("documentNote")}</p>
        <Button type="submit" className="self-start">
          {t("add")}
        </Button>
        {error ? <p role="alert">{error}</p> : null}
      </form>
    </Section>
  );
}

function SecuritySection({
  sessions,
  onReload,
  errorText,
}: { sessions: Session[]; onReload: () => Promise<void> } & WithError) {
  const t = useTranslations("account.manage.security");
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null);
  return (
    <Section title={t("title")}>
      <form
        className="flex flex-col gap-4"
        onSubmit={async (e) => {
          e.preventDefault();
          const r = await account("/password", {
            method: "POST",
            body: { currentPassword: current, newPassword: next },
          });
          if (r.ok) {
            setMsg({ ok: true, text: t("passwordChanged") });
            setCurrent("");
            setNext("");
            await onReload();
          } else
            setMsg({ ok: false, text: errorText(r.problem.fields[0]?.code ?? r.problem.code) });
        }}
      >
        <Field label={t("currentPassword")} required>
          <Input
            type="password"
            autoComplete="current-password"
            value={current}
            onChange={(e) => setCurrent(e.target.value)}
          />
        </Field>
        <Field label={t("newPassword")} required>
          <Input
            type="password"
            autoComplete="new-password"
            value={next}
            onChange={(e) => setNext(e.target.value)}
          />
        </Field>
        <Button type="submit" className="self-start">
          {t("changePassword")}
        </Button>
        {msg ? <p role={msg.ok ? "status" : "alert"}>{msg.text}</p> : null}
      </form>
      <h3 className="font-medium">{t("sessions")}</h3>
      <ul className="flex flex-col gap-2">
        {sessions.map((s) => (
          <li key={s.id} className="flex items-center justify-between gap-3">
            <span>
              {s.userAgent ?? s.id}
              {s.current ? <strong> · {t("current")}</strong> : null}
            </span>
            {s.current ? null : (
              <Button
                variant="ghost"
                size="sm"
                onClick={async () => {
                  await account(`/sessions/${s.id}`, { method: "DELETE" });
                  await onReload();
                }}
              >
                {t("revoke")}
              </Button>
            )}
          </li>
        ))}
      </ul>
      {sessions.length > 1 ? (
        <Button
          variant="secondary"
          className="self-start"
          onClick={async () => {
            await account("/sessions/revoke-others", { method: "POST", body: {} });
            await onReload();
          }}
        >
          {t("revokeOthers")}
        </Button>
      ) : (
        <p className="text-sm text-ink-muted">{t("noSessions")}</p>
      )}
    </Section>
  );
}

function DeleteSection({ onDeleted, errorText }: { onDeleted: () => void } & WithError) {
  const t = useTranslations("account.manage.delete");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  return (
    <Section title={t("title")}>
      <p>{t("body")}</p>
      <form
        className="flex flex-col gap-4"
        onSubmit={async (e) => {
          e.preventDefault();
          const r = await account("", { method: "DELETE", body: { password } });
          if (r.ok) onDeleted();
          else setError(errorText(r.problem.code));
        }}
      >
        <Field label={t("confirm")} required>
          <Input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </Field>
        <Button type="submit" variant="danger" className="self-start">
          {t("button")}
        </Button>
        {error ? <p role="alert">{error}</p> : null}
      </form>
    </Section>
  );
}

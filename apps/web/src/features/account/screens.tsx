"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Alert, Button, Field, Input } from "@dhathuru/ui";
import { AccountCard } from "./AccountCard";
import { AuthForm } from "./AuthForm";
import { postJson } from "./post-json";

export function safeReturnTo(value: string | null | undefined): string {
  if (!value || !value.startsWith("/") || value.startsWith("//") || value.includes("\\"))
    return "/";
  return value;
}

export function LoginScreen({ returnTo }: { returnTo?: string }) {
  const t = useTranslations("account");
  const router = useRouter();
  return (
    <AccountCard title={t("login.title")} subtitle={t("login.subtitle")}>
      <AuthForm
        endpoint="/api/auth/login"
        fields={["email", "password"]}
        submitLabel={t("login.submit")}
        onSuccess={() => {
          router.replace(safeReturnTo(returnTo));
          router.refresh();
        }}
        footer={
          <div className="flex flex-col gap-2 text-sm">
            <Link href="/forgot-password" className="underline">
              {t("login.forgot")}
            </Link>
            <p>
              {t("login.noAccount")}{" "}
              <Link href="/signup" className="font-medium underline">
                {t("login.register")}
              </Link>
            </p>
          </div>
        }
      />
    </AccountCard>
  );
}

export function RegisterScreen() {
  const t = useTranslations("account");
  const [sent, setSent] = useState(false);
  if (sent) {
    return (
      <AccountCard title={t("register.sentTitle")}>
        <p role="status">{t("register.sentBody")}</p>
      </AccountCard>
    );
  }
  return (
    <AccountCard title={t("register.title")} subtitle={t("register.subtitle")}>
      <AuthForm
        endpoint="/api/auth/register"
        fields={["displayName", "email", "newPassword"]}
        rename={{ newPassword: "password" }}
        submitLabel={t("register.submit")}
        onSuccess={() => setSent(true)}
        footer={
          <p className="text-sm">
            {t("register.haveAccount")}{" "}
            <Link href="/login" className="font-medium underline">
              {t("register.login")}
            </Link>
          </p>
        }
      />
    </AccountCard>
  );
}

export function ForgotPasswordScreen() {
  const t = useTranslations("account");
  const [sent, setSent] = useState(false);
  return (
    <AccountCard title={t("forgot.title")} subtitle={sent ? undefined : t("forgot.subtitle")}>
      {sent ? (
        <div className="flex flex-col gap-4">
          <p role="status">{t("forgot.sent")}</p>
          <Link href="/login" className="underline">
            {t("forgot.back")}
          </Link>
        </div>
      ) : (
        <AuthForm
          endpoint="/api/auth/forgot-password"
          fields={["email"]}
          submitLabel={t("forgot.submit")}
          onSuccess={() => setSent(true)}
        />
      )}
    </AccountCard>
  );
}

export function ResetPasswordScreen({ token }: { token?: string }) {
  const t = useTranslations("account");
  const [done, setDone] = useState(false);
  return (
    <AccountCard title={t("reset.title")}>
      {!token ? (
        <Alert tone="caution">{t("reset.missing")}</Alert>
      ) : done ? (
        <div className="flex flex-col gap-4">
          <p role="status">{t("reset.done")}</p>
          <Button asChild>
            <Link href="/login">{t("reset.signIn")}</Link>
          </Button>
        </div>
      ) : (
        <AuthForm
          endpoint="/api/auth/reset-password"
          fields={["newPassword"]}
          extra={{ token }}
          submitLabel={t("reset.submit")}
          onSuccess={() => setDone(true)}
        />
      )}
    </AccountCard>
  );
}

type VerifyState = "working" | "done" | "failed";

// One request per token. React's development double-mount (and a quick reload) would otherwise spend the single-use
// token twice and show "expired" for a link that worked.
const verifications = new Map<string, ReturnType<typeof verifyToken>>();

async function verifyToken(token: string) {
  const result = await postJson("/api/auth/verify-email", { token });
  if (result.ok) await postJson("/api/auth/refresh-session", {}); // signed-in users get a fresh token claim
  return result;
}

export function VerifyEmailScreen({ token }: { token?: string }) {
  const t = useTranslations("account");
  const [state, setState] = useState<VerifyState>(token ? "working" : "failed");
  const [email, setEmail] = useState("");
  const [resent, setResent] = useState(false);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    const request = verifications.get(token) ?? verifyToken(token);
    verifications.set(token, request);
    void request.then((result) => {
      if (!cancelled) setState(result.ok ? "done" : "failed");
    });
    return () => {
      cancelled = true;
    };
  }, [token]);

  return (
    <AccountCard title={t("verify.title")}>
      {state === "working" ? <p role="status">{t("verify.working")}</p> : null}
      {state === "done" ? (
        <div className="flex flex-col gap-4">
          <p role="status">{t("verify.done")}</p>
          <Button asChild>
            <Link href="/">{t("verify.continue")}</Link>
          </Button>
        </div>
      ) : null}
      {state === "failed" ? (
        <div className="flex flex-col gap-4">
          <Alert tone="caution">{token ? t("verify.failed") : t("verify.missing")}</Alert>
          <form
            className="flex flex-col gap-4"
            onSubmit={async (e) => {
              e.preventDefault();
              await postJson("/api/auth/resend-verification", { email });
              setResent(true);
            }}
          >
            <Field label={t("fields.email")} required requiredLabel={t("fields.required")}>
              <Input
                type="email"
                name="email"
                autoComplete="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </Field>
            <Button type="submit" variant="secondary">
              {t("verify.resend")}
            </Button>
            {resent ? <p role="status">{t("verify.resent")}</p> : null}
          </form>
        </div>
      ) : null}
    </AccountCard>
  );
}

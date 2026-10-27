/** Cookie names and attributes for the BFF session. Tokens never reach browser JavaScript. */
export const ACCESS_COOKIE = "dh_at";
export const REFRESH_COOKIE = "dh_rt";

export type CookieAttributes = {
  httpOnly: true;
  secure: boolean;
  sameSite: "lax";
  path: "/";
  maxAge: number;
};

export function cookieAttributes(
  maxAgeSeconds: number,
  secure = process.env.NODE_ENV === "production",
): CookieAttributes {
  return { httpOnly: true, secure, sameSite: "lax", path: "/", maxAge: maxAgeSeconds };
}

/** Refresh token lifetime mirrored from the API's idle window; the API enforces the real limit. */
export const REFRESH_MAX_AGE_SECONDS = 60 * 60 * 24 * 14;

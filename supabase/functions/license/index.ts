import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import postgres from "postgres";

const cors = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), {
  status,
  headers: { ...cors, "content-type": "application/json", "cache-control": "no-store" },
});

const normalizeKey = (value: string) => value.trim().toUpperCase().replace(/\s+/g, "");
const bytesToHex = (bytes: Uint8Array) => [...bytes].map((x) => x.toString(16).padStart(2, "0")).join("");
const base64Url = (bytes: Uint8Array) => btoa(String.fromCharCode(...bytes)).replaceAll("+", "-").replaceAll("/", "_").replace(/=+$/, "");
const utf8 = (value: string) => new TextEncoder().encode(value);

async function hmac(value: string, pepper: string) {
  const key = await crypto.subtle.importKey("raw", utf8(pepper), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  return new Uint8Array(await crypto.subtle.sign("HMAC", key, utf8(value)));
}

async function importSigningKey(pem: string) {
  const body = pem.replace(/-----[^-]+-----/g, "").replace(/\s/g, "");
  const binary = Uint8Array.from(atob(body), (c) => c.charCodeAt(0));
  return crypto.subtle.importKey("pkcs8", binary, { name: "ECDSA", namedCurve: "P-256" }, false, ["sign"]);
}

async function issueToken(privateKey: string, payload: Record<string, unknown>) {
  const header = base64Url(utf8(JSON.stringify({ alg: "ES256", typ: "CVLT" })));
  const data = `${header}.${base64Url(utf8(JSON.stringify(payload)))}`;
  const key = await importSigningKey(privateKey);
  const signature = new Uint8Array(await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, key, utf8(data)));
  return `${data}.${base64Url(signature)}`;
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return new Response(null, { headers: cors });
  if (req.method !== "POST") return json({ ok: false, code: "method_not_allowed" }, 405);

  const configuredKey = "sb_publishable_PD_M14OPyanpcQD2EabPbw_dJktIyVD";
  if (req.headers.get("apikey") !== configuredKey) return json({ ok: false, code: "unauthorized" }, 401);

  let body: { operation?: string; licenseKey?: string; hwidHash?: string; appVersion?: string };
  try { body = await req.json(); } catch { return json({ ok: false, code: "invalid_request" }, 400); }
  if (!['activate', 'validate'].includes(body.operation ?? "") || !/^[a-f0-9]{64}$/.test(body.hwidHash ?? "")) {
    return json({ ok: false, code: "invalid_request", message: "A valid license key and device fingerprint are required." }, 400);
  }

  const licenseKey = normalizeKey(body.licenseKey ?? "");
  if (!/^CLIP-[A-Z0-9]{4}(?:-[A-Z0-9]{4}){3}$/.test(licenseKey)) {
    return json({ ok: false, code: "invalid_license", message: "The license key format is invalid." }, 403);
  }

  const sql = postgres(Deno.env.get("SUPABASE_DB_URL")!, { prepare: false, max: 2 });
  try {
    const secrets = await sql<{ name: string; decrypted_secret: string }[]>`
      select name, decrypted_secret from vault.decrypted_secrets
      where name in ('clipvault_license_pepper', 'clipvault_license_signing_private_key')`;
    const secret = Object.fromEntries(secrets.map((x) => [x.name, x.decrypted_secret]));
    if (!secret.clipvault_license_pepper || !secret.clipvault_license_signing_private_key) return json({ ok: false, code: "server_configuration" }, 503);

    const requestSource = `${req.headers.get("x-forwarded-for") ?? "unknown"}|${req.headers.get("user-agent") ?? "unknown"}`;
    const requestHash = bytesToHex(await hmac(requestSource, secret.clipvault_license_pepper));
    const keyHash = bytesToHex(await hmac(licenseKey, secret.clipvault_license_pepper));

    const result = await sql.begin(async (tx) => {
      const window = new Date(Math.floor(Date.now() / 600_000) * 600_000).toISOString();
      await tx`delete from private.license_rate_limits where window_start < now() - interval '1 day'`;
      const rates = await tx<{ request_count: number }[]>`
        insert into private.license_rate_limits (request_hash, window_start, request_count)
        values (${requestHash}, ${window}, 1)
        on conflict (request_hash, window_start) do update set request_count = private.license_rate_limits.request_count + 1
        returning request_count`;
      if (rates[0].request_count > 30) return { error: "rate_limited", status: 429 };

      const rows = await tx<{ id: string; hwid_hash: string | null; status: string; expires_at: string | null; offline_grace_days: number }[]>`
        select id, hwid_hash, status, expires_at, offline_grace_days
        from private.licenses where key_hash = decode(${keyHash}, 'hex') for update`;
      if (!rows.length) return { error: "invalid_license", status: 403 };
      const license = rows[0];
      const expired = license.expires_at && new Date(license.expires_at).getTime() <= Date.now();
      if (license.status !== "active" || expired) {
        const code = expired ? "expired" : license.status;
        await tx`insert into private.license_activation_logs (license_id, action, result) values (${license.id}, ${body.operation!}, ${code})`;
        return { error: code, status: 403 };
      }
      if (license.hwid_hash && license.hwid_hash !== body.hwidHash) {
        await tx`insert into private.license_activation_logs (license_id, action, result) values (${license.id}, ${body.operation!}, 'device_mismatch')`;
        return { error: "device_mismatch", status: 409 };
      }
      await tx`update private.licenses set hwid_hash = coalesce(hwid_hash, ${body.hwidHash!}), activated_at = coalesce(activated_at, now()), last_seen_at = now() where id = ${license.id}`;
      await tx`insert into private.license_activation_logs (license_id, action, result) values (${license.id}, ${body.operation!}, 'allowed')`;
      return { license, status: 200 };
    });

    if ("error" in result) {
      const messages: Record<string, string> = {
        invalid_license: "The license key you entered is invalid.",
        device_mismatch: "This license is already activated on another computer.",
        expired: "Your ClipVault license has expired.",
        revoked: "This license has been revoked.",
        suspended: "This license has been suspended.",
        rate_limited: "Too many activation attempts. Please wait and try again.",
      };
      return json({ ok: false, code: result.error, message: messages[result.error] ?? "The license cannot be used." }, result.status);
    }
    const serverTime = new Date();
    const licenseExpiry = result.license.expires_at ? new Date(result.license.expires_at).getTime() : Number.MAX_SAFE_INTEGER;
    const graceExpiry = serverTime.getTime() + result.license.offline_grace_days * 86_400_000;
    const expiresAt = Math.min(licenseExpiry, graceExpiry);
    const token = await issueToken(secret.clipvault_license_signing_private_key, {
      v: 1, licenseId: result.license.id, hwidHash: body.hwidHash,
      iat: Math.floor(serverTime.getTime() / 1000), exp: Math.floor(expiresAt / 1000),
    });
    return json({ ok: true, status: "active", token, serverTime: serverTime.toISOString(), expiresAt: new Date(expiresAt).toISOString(), licenseExpiresAt: result.license.expires_at });
  } catch (error) {
    console.error("License request failed", error instanceof Error ? error.message : "unknown");
    return json({ ok: false, code: "service_unavailable", message: "The licensing service is temporarily unavailable." }, 503);
  } finally { await sql.end({ timeout: 2 }); }
});

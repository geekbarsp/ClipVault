import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import postgres from "postgres";

const cors = { "Access-Control-Allow-Origin": "*", "Access-Control-Allow-Headers": "authorization, apikey, content-type", "Access-Control-Allow-Methods": "POST, OPTIONS" };
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { ...cors, "content-type": "application/json", "cache-control": "no-store" } });
const utf8 = (v: string) => new TextEncoder().encode(v);
const hex = (v: Uint8Array) => [...v].map((x) => x.toString(16).padStart(2, "0")).join("");
async function digest(key: string, pepper: string) {
  const k = await crypto.subtle.importKey("raw", utf8(pepper), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  return hex(new Uint8Array(await crypto.subtle.sign("HMAC", k, utf8(key))));
}
const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
function newKey() {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  const chars = [...bytes].map((x) => alphabet[x % alphabet.length]).join("");
  return `CLIP-${chars.slice(0,4)}-${chars.slice(4,8)}-${chars.slice(8,12)}-${chars.slice(12,16)}`;
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return new Response(null, { headers: cors });
  if (req.method !== "POST") return json({ ok: false, code: "method_not_allowed" }, 405);
  const projectUrl = Deno.env.get("SUPABASE_URL")!;
  const publishableKey = "sb_publishable_PD_M14OPyanpcQD2EabPbw_dJktIyVD";
  const auth = req.headers.get("authorization") ?? "";
  if (!auth.startsWith("Bearer ") || req.headers.get("apikey") !== publishableKey) return json({ ok: false, code: "unauthorized" }, 401);
  const userResponse = await fetch(`${projectUrl}/auth/v1/user`, { headers: { authorization: auth, apikey: publishableKey } });
  if (!userResponse.ok) return json({ ok: false, code: "unauthorized" }, 401);
  const user = await userResponse.json();

  const sql = postgres(Deno.env.get("SUPABASE_DB_URL")!, { prepare: false, max: 2 });
  try {
    const admins = await sql`select 1 from private.license_admins where user_id = ${user.id}::uuid`;
    if (!admins.length) return json({ ok: false, code: "forbidden" }, 403);
    const body = await req.json();
    const action = String(body.action ?? "list");
    const secrets = await sql<{ decrypted_secret: string }[]>`select decrypted_secret from vault.decrypted_secrets where name = 'clipvault_license_pepper'`;
    const pepper = secrets[0]?.decrypted_secret;
    if (!pepper) return json({ ok: false, code: "server_configuration" }, 503);

    if (action === "list") {
      const rows = await sql`select id, key_hint, hwid_hash, status, created_at, activated_at, expires_at, last_seen_at, offline_grace_days from private.licenses order by created_at desc limit 500`;
      return json({ ok: true, licenses: rows });
    }
    if (action === "events") {
      const rows = await sql`select id, license_id, action, result, created_at from private.license_activation_logs order by created_at desc limit 500`;
      return json({ ok: true, events: rows });
    }
    if (action === "create" || action === "create_batch") {
      const count = action === "create" ? 1 : Math.max(1, Math.min(100, Number(body.count ?? 1)));
      const days = body.validDays == null ? null : Math.max(1, Math.min(3650, Number(body.validDays)));
      const grace = Math.max(0, Math.min(30, Number(body.offlineGraceDays ?? 7)));
      const keys: string[] = [];
      await sql.begin(async (tx) => {
        for (let i = 0; i < count; i++) {
          const key = newKey();
          const keyHash = await digest(key, pepper);
          await tx`insert into private.licenses (key_hash, key_hint, expires_at, offline_grace_days) values (decode(${keyHash}, 'hex'), ${`CLIP-****-****-****-${key.slice(-4)}`}, ${days ? new Date(Date.now() + days * 86400000).toISOString() : null}, ${grace})`;
          keys.push(key);
        }
      });
      return json({ ok: true, keys });
    }
    const id = String(body.id ?? "");
    if (!/^[0-9a-f-]{36}$/i.test(id)) return json({ ok: false, code: "invalid_id" }, 400);
    if (action === "reset_hwid") await sql`update private.licenses set hwid_hash = null, activated_at = null where id = ${id}::uuid`;
    else if (action === "set_status" && ["active", "suspended", "revoked"].includes(body.status)) await sql`update private.licenses set status = ${body.status} where id = ${id}::uuid`;
    else if (action === "set_expiration") await sql`update private.licenses set expires_at = ${body.expiresAt || null} where id = ${id}::uuid`;
    else return json({ ok: false, code: "invalid_action" }, 400);
    await sql`insert into private.license_activation_logs (license_id, action, result) values (${id}::uuid, ${`admin:${action}`}, 'completed')`;
    return json({ ok: true });
  } catch (error) {
    console.error("Admin license operation failed", error instanceof Error ? error.message : "unknown");
    return json({ ok: false, code: "service_unavailable" }, 503);
  } finally { await sql.end({ timeout: 2 }); }
});

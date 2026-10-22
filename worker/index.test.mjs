// ポートフォリオの Worker（worker/index.js）の、報告の経路とメンテナンス中の 503 のテスト。  node --test worker/index.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import worker from "./index.js";

const HOST = "https://fara-labs.com";

/** 静的アセットの代わり。ここまで来たら 418 を返す（API の経路で、アセットに流れていないことを確かめる）。 */
const ASSETS = { fetch: async () => new Response("asset", { status: 418 }) };

const run = (path, init = {}, env = {}) => worker.fetch(new Request(`${HOST}${path}`, init), { ASSETS, ...env });

test("報告の設定（config）は、設定が無いとき enabled: false を返す（シークレットは返さない）", async () => {
  const res = await run("/api/report/config");
  assert.equal(res.status, 200);
  assert.deepEqual(await res.json(), { enabled: false, siteKey: null });
});

test("報告の設定（config）は、サイトキーだけを返す", async () => {
  const res = await run("/api/report/config", {}, { TURNSTILE_SITE_KEY: "site-key", TURNSTILE_SECRET: "secret-value", DISCORD_WEBHOOK_URL: "https://discord.example/x" });
  const body = await res.json();
  assert.deepEqual(body, { enabled: true, siteKey: "site-key" });
  assert.ok(!JSON.stringify(body).includes("secret-value"));
});

test("メンテナンス中（MAINTENANCE=true）は、報告の受け口も 503", async () => {
  const res = await run("/api/report", { method: "POST", headers: { "Content-Type": "application/json" }, body: "{}" }, { MAINTENANCE: "true" });
  assert.equal(res.status, 503);
  assert.equal(res.headers.get("Cache-Control"), "no-store");
});

test("メンテナンス中でも、報告の設定（config）は 503（API はまとめて止まる）", async () => {
  const res = await run("/api/report/config", {}, { MAINTENANCE: "true" });
  assert.equal(res.status, 503);
});

test("報告以外の /api/ は、静的アセットに任せる（今までどおり）", async () => {
  const res = await run("/api/something-else");
  assert.equal(res.status, 418);
});

test("メンテナンスの状態は、通常のとき off", async () => {
  const res = await run("/api/maintenance");
  assert.deepEqual(await res.json(), { state: "off", message: "" });
});

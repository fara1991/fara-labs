// ポートフォリオの Worker（worker/index.js）のテスト: 報告の経路、メンテナンス中の 503、お知らせの帯の挿入。  node --test worker/index.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import worker from "./index.js";

const HOST = "https://fara-labs.com";

// Node には Workers の HTMLRewriter が無いので、テストだけに、.maintenance-slot の中に append する最小限の代わりを入れる（本番の Worker では使われない）
globalThis.HTMLRewriter = class {
  handlers = null;
  on(_selector, handlers) { this.handlers = handlers; return this; }
  transform(response) {
    let inserted = "";
    this.handlers.element({ append(html) { inserted += html; } });
    const body = new ReadableStream({
      async start(controller) {
        const text = await response.text();
        controller.enqueue(new TextEncoder().encode(text.replace('<div class="maintenance-slot"></div>', `<div class="maintenance-slot">${inserted}</div>`)));
        controller.close();
      },
    });
    return new Response(body, { status: response.status, headers: response.headers });
  }
};

/** 静的アセットの代わり。ここまで来たら 418 を返す（API の経路で、アセットに流れていないことを確かめる）。 */
const ASSETS = { fetch: async () => new Response("asset", { status: 418 }) };

/** 静的アセットの代わりに HTML を返す（お知らせの帯の挿入を確かめる）。 */
const HTML_PAGE = {
  fetch: async () => new Response('<!doctype html><html><body><header>H</header><div class="maintenance-slot"></div><div class="site-scroll"><main>本文</main></div></body></html>', {
    headers: { "Content-Type": "text/html; charset=utf-8" },
  }),
};

const run = (path, init = {}, env = {}, assets = ASSETS) => worker.fetch(new Request(`${HOST}${path}`, init), { ASSETS: assets, ...env });

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

test("メンテナンス中のページは、HTML のメンテナンス画面を 503 で返す（アセットには流さない）", async () => {
  const res = await run("/works", { headers: { Accept: "text/html" } }, { MAINTENANCE: "true" }, HTML_PAGE);
  assert.equal(res.status, 503);
  assert.equal(res.headers.get("X-Robots-Tag"), "noindex");
  assert.match(await res.text(), /ただいまメンテナンス中です/);
});

test("報告以外の /api/ は、404（静的アセットには流さない）", async () => {
  const res = await run("/api/something-else");
  assert.equal(res.status, 404);
});

test("通常のとき、ページはそのままアセットから返す（お知らせの帯は入れない）", async () => {
  const res = await run("/", {}, {}, HTML_PAGE);
  assert.equal(res.status, 200);
  const html = await res.text();
  assert.ok(!html.includes("maintenance-notice"));
});

test("お知らせ（MAINTENANCE=notice）のとき、帯は .maintenance-slot の中に入る（本文の中には入らない）", async () => {
  const res = await run("/", {}, { MAINTENANCE: "notice", MAINTENANCE_MESSAGE: "10月25日（土）20:00〜21:00 ごろ、サイトを更新します" }, HTML_PAGE);
  const html = await res.text();
  assert.equal(res.headers.get("Cache-Control"), "no-store");
  assert.match(html, /<div class="maintenance-slot"><div class="maintenance-notice" role="status">/);
  assert.match(html, /10月25日（土）20:00〜21:00 ごろ、サイトを更新します/);
  assert.ok(html.indexOf("maintenance-notice") < html.indexOf('class="site-scroll"'));
});

test("お知らせは、HTML 以外（画像など）には付けない", async () => {
  const image = { fetch: async () => new Response("png", { headers: { "Content-Type": "image/png" } }) };
  const res = await run("/images/logo.png", {}, { MAINTENANCE: "notice" }, image);
  assert.equal(await res.text(), "png");
});

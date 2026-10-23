// tools/build-pages.mjs のテスト（kit の build-pages に渡す入力・出力の形）。  node --test tools/build-pages.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, writeFileSync, readFileSync, existsSync, mkdirSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { run, toKitPages } from "./build-pages.mjs";

const TEMPLATE = `<!DOCTYPE html><html lang="ja"><head><!-- SEO-HEAD:START --><title>x</title><!-- SEO-HEAD:END --></head>
<body><div id="boot-indicator"></div><div id="app"><!-- SEO-BODY:START --><!-- SEO-BODY:END --></div></body></html>`;

const PAGES = [
  { path: "/", title: "Fara | Portfolio", description: "紹介", bodyHtml: '<div class="entry-static"><main>ホーム</main></div>' },
  { path: "/works", title: "Works | Fara", description: "作品", bodyHtml: '<div class="entry-static"><main>作品</main></div>' },
];

function setup(pages = PAGES) {
  const root = mkdtempSync(join(tmpdir(), "pf-build-pages-"));
  const dir = join(root, "wwwroot");
  mkdirSync(dir);
  writeFileSync(join(dir, "index.html"), TEMPLATE);
  const pagesFile = join(root, "pages.json");
  writeFileSync(pagesFile, JSON.stringify(pages));
  return { root, dir, pagesFile };
}

test("toKitPages: すべてのページに og:image（これまでと同じ logo.png）を付ける", () => {
  assert.ok(toKitPages(PAGES).every((p) => p.ogImage === "/images/logo.png"));
});

test("toKitPages: 空の一覧はエラー（PrerenderPages を先に実行していない）", () => {
  assert.throws(() => toKitPages([]), /pages\.json が空/);
});

test("production: 入口ページ（ホームは index.html）・404.html・robots.txt・sitemap.xml・_headers を作る", () => {
  const { root, dir, pagesFile } = setup();
  try {
    run({ dir, env: "production", pagesFile });
    const index = readFileSync(join(dir, "index.html"), "utf8");
    assert.match(index, /<title>Fara \| Portfolio<\/title>/);
    assert.match(index, /<link rel="canonical" href="https:\/\/fara-labs\.com\/" \/>/);
    assert.match(index, /<main>ホーム<\/main>/);
    assert.match(readFileSync(join(dir, "works.html"), "utf8"), /<main>作品<\/main>/);
    assert.ok(existsSync(join(dir, "404.html")));
    assert.match(readFileSync(join(dir, "sitemap.xml"), "utf8"), /<loc>https:\/\/fara-labs\.com\/works<\/loc>/);
    assert.match(readFileSync(join(dir, "robots.txt"), "utf8"), /Sitemap: https:\/\/fara-labs\.com\/sitemap\.xml/);
    const headers = readFileSync(join(dir, "_headers"), "utf8");
    assert.match(headers, /Content-Security-Policy:/);
    assert.match(headers, /static\.cloudflareinsights\.com/);
    assert.match(headers, /challenges\.cloudflare\.com/);
    assert.match(headers, /'wasm-unsafe-eval'/);
    assert.ok(!/X-Robots-Tag/.test(headers));
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("develop: 確認用の URL を canonical にし、noindex ヘッダーを付け、sitemap は作らない", () => {
  const { root, dir, pagesFile } = setup();
  try {
    run({ dir, env: "develop", pagesFile });
    assert.match(readFileSync(join(dir, "index.html"), "utf8"), /href="https:\/\/dev\.fara-labs\.com\/"/);
    assert.match(readFileSync(join(dir, "_headers"), "utf8"), /X-Robots-Tag: noindex/);
    assert.ok(!existsSync(join(dir, "sitemap.xml")));
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("本文の入った index.html（作成済みの出力）は、テンプレートにしない（もう一度 dotnet publish してから）", () => {
  const { root, dir, pagesFile } = setup();
  try {
    run({ dir, env: "production", pagesFile });
    assert.throws(() => run({ dir, env: "production", pagesFile }), /dotnet publish/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("--env が不正なときはエラー", () => {
  const { root, dir, pagesFile } = setup();
  try {
    assert.throws(() => run({ dir, env: "staging", pagesFile }), /--env は/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

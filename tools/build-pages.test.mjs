// tools/build-pages.mjs のテスト（kit の build-pages に渡す入力・出力の形）。  node --test tools/build-pages.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, writeFileSync, readFileSync, existsSync, mkdirSync, rmSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { run, toKitPages, toNotFoundPage, adjustFiles } from "./build-pages.mjs";
import { prerenderArgs } from "./prerender-pages.mjs";

const SITE = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), "site.json"), "utf8"));

test("prerender-pages: ホスト名は tools/site.json から（本番は origin、確認用は devOrigin）", () => {
  const prod = prerenderArgs({ env: "production", site: SITE });
  assert.equal(prod[prod.indexOf("--host") + 1], "fara-labs.com");
  const dev = prerenderArgs({ env: "develop", site: SITE });
  assert.equal(dev[dev.indexOf("--host") + 1], "dev.fara-labs.com");
  assert.equal(prod[prod.indexOf("--out") + 1], "artifacts/pages.json");
});

test("prerender-pages: --env が不正なとき、設定の値が無いときはエラー", () => {
  assert.throws(() => prerenderArgs({ env: "staging", site: SITE }), /--env は/);
  assert.throws(() => prerenderArgs({ env: "develop", site: { origin: "https://fara-labs.com" } }), /devOrigin/);
});

test("og:image: 行が見つからない HTML（kit の出力の形が変わった）は、ビルドを失敗させる（黙って抜けない）", () => {
  const files = new Map([["works.html", "<html><head><title>x</title></head></html>"]]);
  assert.throws(() => adjustFiles(files), /works\.html に og:image の行が見つかりません/);
});

test("og:image: 404.html は共有カードの画像を持たないので、行が無くても失敗しない", () => {
  const files = new Map([["404.html", "<html><head></head></html>"]]);
  assert.doesNotThrow(() => adjustFiles(files));
});

test("og:image: 行があれば、大きさ・説明・twitter:image を足す", () => {
  const html = '<head>\n  <meta property="og:image" content="https://fara-labs.com/og.png?v=2" />\n</head>';
  const out = adjustFiles(new Map([["index.html", html]])).get("index.html");
  assert.match(out, /og:image:width" content="1200"/);
  assert.match(out, /twitter:image" content="https:\/\/fara-labs\.com\/og\.png\?v=2"/);
});

const TEMPLATE = `<!DOCTYPE html><html lang="ja"><head><!-- SEO-HEAD:START --><title>x</title><!-- SEO-HEAD:END --></head>
<body><div id="boot-indicator"></div><div id="app"><!-- SEO-BODY:START --><!-- SEO-BODY:END --></div></body></html>`;

const PAGES = [
  { path: "/", title: "Fara Labs", description: "紹介", bodyHtml: '<div class="entry-static"><main>ホーム</main></div>' },
  { path: "/works", title: "Works | Fara", description: "作品", bodyHtml: '<div class="entry-static"><main>作品</main></div>' },
];

function setup(pages = PAGES) {
  // 作業フォルダは artifacts/（Git に入れない）の中。システムの一時フォルダは使わない
  const parent = join(dirname(fileURLToPath(import.meta.url)), "..", "artifacts", "test");
  mkdirSync(parent, { recursive: true });
  const root = mkdtempSync(join(parent, "pf-build-pages-"));
  const dir = join(root, "wwwroot");
  mkdirSync(dir);
  writeFileSync(join(dir, "index.html"), TEMPLATE);
  const pagesFile = join(root, "pages.json");
  writeFileSync(pagesFile, JSON.stringify(pages));
  return { root, dir, pagesFile };
}

test("toKitPages: すべてのページに og:image（?v 付きの 1200×630 の画像）を付ける", () => {
  assert.ok(toKitPages(PAGES).every((p) => p.ogImage === "/og.png?v=2"));
});

test("toKitPages: 空の一覧はエラー（PrerenderPages を先に実行していない）", () => {
  assert.throws(() => toKitPages([]), /pages\.json が空/);
});

test("production: 入口ページ（ホームは index.html）・404.html・robots.txt・sitemap.xml・_headers を作る", () => {
  const { root, dir, pagesFile } = setup();
  try {
    run({ dir, env: "production", pagesFile });
    const index = readFileSync(join(dir, "index.html"), "utf8");
    assert.match(index, /<title>Fara Labs<\/title>/);
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
    assert.ok(!/X-Robots-Tag/.test(headers.split("/data/*")[0]), "production の全体は noindex にしない");
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

test("共有カード: summary_large_image + og:image（絶対 URL）・大きさ・説明・twitter:image", () => {
  const { root, dir, pagesFile } = setup();
  try {
    run({ dir, env: "production", pagesFile });
    const html = readFileSync(join(dir, "index.html"), "utf8");
    assert.match(html, /property="og:image" content="https:\/\/fara-labs\.com\/og\.png\?v=2"/);
    assert.match(html, /property="og:image:width" content="1200"/);
    assert.match(html, /property="og:image:height" content="630"/);
    assert.match(html, /property="og:image:alt" content="[^"]+"/);
    assert.match(html, /name="twitter:image" content="https:\/\/fara-labs\.com\/og\.png\?v=2"/);
    assert.match(html, /name="twitter:card" content="summary_large_image" \/>/);
    assert.ok(!/content="summary"/.test(html));
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("共有カード（develop）: og:image は確認用の URL", () => {
  const { root, dir, pagesFile } = setup();
  try {
    run({ dir, env: "develop", pagesFile });
    const html = readFileSync(join(dir, "works.html"), "utf8");
    assert.match(html, /property="og:image" content="https:\/\/dev\.fara-labs\.com\/og\.png\?v=2"/);
    assert.match(html, /name="twitter:image" content="https:\/\/dev\.fara-labs\.com\/og\.png\?v=2"/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("_headers: _framework の指紋付きファイルだけ長期キャッシュ（immutable）、/data/* は noindex", () => {
  const { root, dir, pagesFile } = setup();
  try {
    run({ dir, env: "production", pagesFile });
    const headers = readFileSync(join(dir, "_headers"), "utf8");
    for (const path of ["/_framework/*.wasm", "/_framework/dotnet.native.*.js", "/_framework/dotnet.runtime.*.js"]) {
      assert.ok(headers.includes(`${path}\n  Cache-Control: public, max-age=31536000, immutable`), path);
    }
    // 画像・アイコンは固定名（版は ?v=N）なので、長期キャッシュにしない（must-revalidate の既定のまま）
    assert.ok(!/\/(og|favicon|apple-touch)/.test(headers));
    // 指紋の無い blazor.webassembly.js・dotnet.js は、長期キャッシュにしない（デプロイ後に古いファイルが残るため）
    assert.ok(!/_framework\/\*(?!\.wasm)/.test(headers.replace(/_framework\/(dotnet\.native|dotnet\.runtime)\.\*\.js/g, "")));
    assert.match(headers, /\/data\/\*\n {2}X-Robots-Tag: noindex\n$/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("404.html: Razor の 404 の本文と題名を入れる（JavaScript が無くても読める）", () => {
  const { root, dir, pagesFile } = setup();
  try {
    const notFoundFile = join(root, "not-found.json");
    writeFileSync(notFoundFile, JSON.stringify({ title: "ページが見つかりません | Fara", bodyHtml: '<div class="entry-static"><p>ページが見つかりません</p></div>' }));
    run({ dir, env: "production", pagesFile, notFoundFile });
    const html = readFileSync(join(dir, "404.html"), "utf8");
    assert.match(html, /<title>ページが見つかりません \| Fara<\/title>/);
    assert.match(html, /<p>ページが見つかりません<\/p>/);
    assert.match(html, /name="robots" content="noindex"/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("toNotFoundPage: title か bodyHtml が無ければエラー", () => {
  assert.throws(() => toNotFoundPage({ title: "x" }), /not-found\.json/);
});

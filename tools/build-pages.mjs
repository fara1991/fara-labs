// 入口ページ（本文入りの HTML）・404.html・robots.txt・sitemap.xml・_headers を作る（dotnet publish と tools/PrerenderPages の後に実行する）。
//   node tools/build-pages.mjs [--env production|develop] [--dir publish/wwwroot] [--pages artifacts/pages.json]
//
// 作り方（head・区切り・robots・sitemap・_headers の中身）は kit の build/build-pages.mjs と csp/render-headers.mjs。
// ここに残るのは、サイトの設定（tools/site.json）と、テンプレートの扱い、ページごとの og:image。
// 本文（bodyHtml）は tools/PrerenderPages が Razor のページから作る pages.json。
// テンプレートは --dir の index.html（dotnet publish の出力）。先に artifacts/entry-template へ写してから読む（kit は出力先で上書きする設定を拒否するため）。
import { readFileSync, existsSync, mkdirSync, copyFileSync } from "node:fs";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { buildPages, writeFiles, assertTemplateNotOverwritten } from "../external/fara-site-kit/build/build-pages.mjs";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const SITE_FILE = join(root, "tools", "site.json");
const TEMPLATE_DIR = join(root, "artifacts", "entry-template");
const ENVIRONMENTS = ["production", "develop"];
/** 共有したときに出す画像（1200×630 のダーク版。ライト版は og-v1-light.png に保存してある。絵を変えたら -v2 にする） */
const OG_IMAGE = "/og-v1.png";
const OG_IMAGE_WIDTH = 1200;
const OG_IMAGE_HEIGHT = 630;
const OG_IMAGE_ALT = "Fara Labs。中央の「F」から、作品のアイコンが星座のように線でつながっている";

/** 404.html の本文（Razor の 404 の画面。tools/PrerenderPages が作る not-found.json）。JavaScript が無くても読める。 */
export function toNotFoundPage(file) {
    if (typeof file?.title !== "string" || typeof file?.bodyHtml !== "string" || file.bodyHtml === "") {
        throw new Error("not-found.json に title と bodyHtml が要ります（tools/PrerenderPages を先に実行してください）");
    }
    return { title: file.title, bodyHtml: file.bodyHtml };
}

/**
 * kit の出力に、サイト側の調整を足す（kit は触らない）。
 *  - 共有カード: kit は og:image と twitter:card（summary_large_image）だけを出すので、画像の大きさ・説明（og:image:width / height / alt）と twitter:image を足す
 *  - /data/*.json（サイトの中身）は、検索に出さない（X-Robots-Tag: noindex）
 */
export function adjustFiles(files) {
    const ogImage = /^( *)(<meta property="og:image" content="([^"]*)" \/>)/m;
    for (const [name, content] of files) {
        if (name.endsWith(".html") && typeof content === "string") {
            files.set(name, content.replace(ogImage, (_all, indent, tag, url) => [
                `${indent}${tag}`,
                `<meta property="og:image:width" content="${OG_IMAGE_WIDTH}" />`,
                `<meta property="og:image:height" content="${OG_IMAGE_HEIGHT}" />`,
                `<meta property="og:image:alt" content="${OG_IMAGE_ALT}" />`,
                `<meta name="twitter:image" content="${url}" />`,
            ].join(`\n${indent}`)));
        }
    }
    const headers = files.get("_headers");
    if (typeof headers === "string") {
        files.set("_headers", headers.replace(/\n*$/, "\n") + "\n/data/*\n  X-Robots-Tag: noindex\n");
    }
    return files;
}

/** pages.json（Razor のページから作った path・title・description・bodyHtml の配列）を、kit の入力にする。 */
export function toKitPages(pages) {
    if (!Array.isArray(pages) || pages.length === 0) throw new Error("pages.json が空です（tools/PrerenderPages を先に実行してください）");
    return pages.map((page) => ({ ...page, ogImage: OG_IMAGE }));
}

/** dir（dotnet publish の wwwroot）に、入口ページ・404.html・robots.txt・sitemap.xml・_headers を書き出す。書いたファイル名を返す。 */
export function run({ dir, env, pagesFile, notFoundFile, siteFile = SITE_FILE }) {
    if (!ENVIRONMENTS.includes(env)) throw new Error(`--env は ${ENVIRONMENTS.join(" か ")} を指定してください`);
    const indexPath = join(dir, "index.html");
    if (!existsSync(indexPath)) throw new Error(`${indexPath} がありません（先に dotnet publish してください）`);
    // テンプレートは出力先の外に写してから読む（同じフォルダの index.html は書き換えられるため）
    mkdirSync(TEMPLATE_DIR, { recursive: true });
    const templatePath = join(TEMPLATE_DIR, "index.html");
    copyFileSync(indexPath, templatePath);
    const pages = toKitPages(JSON.parse(readFileSync(pagesFile, "utf8")));
    const site = JSON.parse(readFileSync(siteFile, "utf8"));
    if (notFoundFile) site.notFoundPage = toNotFoundPage(JSON.parse(readFileSync(notFoundFile, "utf8")));
    const template = readFileSync(templatePath, "utf8");
    const files = adjustFiles(buildPages({ site, pages, template, env }));
    assertTemplateNotOverwritten(templatePath, dir, files);
    return writeFiles(resolve(dir), files);
}

function parseArgs(argv) {
    const args = { env: "production", dir: join(root, "publish", "wwwroot"), pagesFile: join(root, "artifacts", "pages.json"), notFoundFile: join(root, "artifacts", "not-found.json") };
    for (let i = 0; i < argv.length; i++) {
        if (argv[i] === "--env") args.env = argv[++i];
        else if (argv[i] === "--dir") args.dir = resolve(argv[++i]);
        else if (argv[i] === "--pages") args.pagesFile = resolve(argv[++i]);
        else if (argv[i] === "--not-found") args.notFoundFile = resolve(argv[++i]);
        else throw new Error(`知らない引数: ${argv[i]}`);
    }
    return args;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    try {
        const written = run(parseArgs(process.argv.slice(2)));
        console.log(`入口ページを書き出しました: ${written.join(", ")}`);
    } catch (error) {
        console.error(error.message);
        process.exitCode = 1;
    }
}

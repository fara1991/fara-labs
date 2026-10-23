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
/** 共有したときに出す画像（これまでの og:image と同じ） */
const OG_IMAGE = "/images/logo.png";

/** pages.json（Razor のページから作った path・title・description・bodyHtml の配列）を、kit の入力にする。 */
export function toKitPages(pages) {
    if (!Array.isArray(pages) || pages.length === 0) throw new Error("pages.json が空です（tools/PrerenderPages を先に実行してください）");
    return pages.map((page) => ({ ...page, ogImage: OG_IMAGE }));
}

/** dir（dotnet publish の wwwroot）に、入口ページ・404.html・robots.txt・sitemap.xml・_headers を書き出す。書いたファイル名を返す。 */
export function run({ dir, env, pagesFile, siteFile = SITE_FILE }) {
    if (!ENVIRONMENTS.includes(env)) throw new Error(`--env は ${ENVIRONMENTS.join(" か ")} を指定してください`);
    const indexPath = join(dir, "index.html");
    if (!existsSync(indexPath)) throw new Error(`${indexPath} がありません（先に dotnet publish してください）`);
    // テンプレートは出力先の外に写してから読む（同じフォルダの index.html は書き換えられるため）
    mkdirSync(TEMPLATE_DIR, { recursive: true });
    const templatePath = join(TEMPLATE_DIR, "index.html");
    copyFileSync(indexPath, templatePath);
    const pages = toKitPages(JSON.parse(readFileSync(pagesFile, "utf8")));
    const site = JSON.parse(readFileSync(siteFile, "utf8"));
    const template = readFileSync(templatePath, "utf8");
    const files = buildPages({ site, pages, template, env });
    assertTemplateNotOverwritten(templatePath, dir, files);
    return writeFiles(resolve(dir), files);
}

function parseArgs(argv) {
    const args = { env: "production", dir: join(root, "publish", "wwwroot"), pagesFile: join(root, "artifacts", "pages.json") };
    for (let i = 0; i < argv.length; i++) {
        if (argv[i] === "--env") args.env = argv[++i];
        else if (argv[i] === "--dir") args.dir = resolve(argv[++i]);
        else if (argv[i] === "--pages") args.pagesFile = resolve(argv[++i]);
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

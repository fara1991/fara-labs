// tools/PrerenderPages（Razor のページを本文の HTML にして artifacts/pages.json と not-found.json を作る）を、
// サイトの設定（tools/site.json の origin・devOrigin）のホスト名で動かす。ホスト名を package.json に直書きしないため。
//   node tools/prerender-pages.mjs --env production|develop
import { readFileSync } from "node:fs";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const SITE_FILE = join(root, "tools", "site.json");
const ENVIRONMENTS = ["production", "develop"];

/** dotnet run に渡す引数。env に応じて、本番（origin）か確認用（devOrigin）のホスト名を使う。 */
export function prerenderArgs({ env, site }) {
  if (!ENVIRONMENTS.includes(env)) throw new Error(`--env は ${ENVIRONMENTS.join(" か ")} を指定してください`);
  const key = env === "develop" ? "devOrigin" : "origin";
  if (typeof site[key] !== "string") throw new Error(`tools/site.json に ${key} がありません`);
  const host = new URL(site[key]).host;
  return [
    "run", "--project", "tools/PrerenderPages", "-c", "Release", "--no-launch-profile", "--",
    "--data", "publish/wwwroot/data",
    "--host", host,
    "--out", "artifacts/pages.json",
    "--not-found-out", "artifacts/not-found.json",
  ];
}

function parseEnv(argv) {
  const i = argv.indexOf("--env");
  return i === -1 ? "production" : argv[i + 1];
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const site = JSON.parse(readFileSync(SITE_FILE, "utf8"));
    const result = spawnSync("dotnet", prerenderArgs({ env: parseEnv(process.argv.slice(2)), site }), { cwd: root, stdio: "inherit" });
    process.exitCode = result.status ?? 1;
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}

#!/usr/bin/env node
// npm run layout-check: publish/wwwroot（npm run publish:site の出力）を kit の簡易サーバーで配り、レイアウトを計測する。
// 公開はしない（デプロイ・Cloudflare へのアクセスは無い）。終了コードは kit の CLI と同じ（0 違反なし / 1 違反あり / 2 計測できなかった）。
import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { startStaticServer } from "../external/fara-blazor-component/tools/layout-check/serve-static.mjs";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const publishDir = join(root, "publish", "wwwroot");
const config = join(root, "layout-check.config.mjs");
const cli = join(root, "external", "fara-blazor-component", "tools", "layout-check", "cli.mjs");
const START_PORT = 8840;

if (!existsSync(join(publishDir, "index.html"))) {
  console.error(`エラー: 計測する静的サイトが無い（${publishDir}）。先に npm run publish:site を実行する`);
  process.exitCode = 2;
} else {
  const server = await startStaticServer(publishDir, START_PORT);
  let code = 2;
  try {
    code = await new Promise((resolve) => {
      const child = spawn(process.execPath, [cli, "--config", config], {
        cwd: root,
        stdio: "inherit",
        env: { ...process.env, LAYOUT_CHECK_BASE_URL: server.origin },
      });
      child.on("error", () => resolve(2));
      child.on("exit", (exitCode) => resolve(exitCode ?? 2));
    });
  } finally {
    await server.close();
  }
  process.exitCode = code;
}

// このサイトの ESLint 設定（flat config）。ルールと、kit との共通の部分は external/fara-blazor-component/tools/lint/eslint.config.shared.mjs（docs/lint.md（1））。
// 対象: tools/・worker/・.github/scripts/・wwwroot/js/。external/**（kit）と生成物（publish・artifacts）は、共通設定の ignores で外す。
import shared, { createSharedConfig } from "./external/fara-blazor-component/tools/lint/eslint.config.shared.mjs";

export default [
  ...shared,
  // 画面の JS（HTML の <script> で読む。import も module もしない）。ブラウザのグローバルを使う
  ...createSharedConfig({
    files: ["src/FaraPortfolio.Web/wwwroot/js/**/*.js"],
    environments: ["browser"],
    sourceType: "script",
  }),
];

// ポートフォリオ（fara-labs.com）の Worker。サイト本体は静的アセットで、/api/* だけがここを通る（wrangler.jsonc の assets.run_worker_first）。
//   GET  /api/report/config・POST /api/report → 作者への報告（worker/report.js。設定は docs/report-and-maintenance.md）
//   GET /api/maintenance → {"state": "off" | "notice" | "on", "message": "..."}（メンテナンスの状態。アプリが起動時に読んで、お知らせやメンテナンス画面を出す）
// メンテナンスの切り替えは、Worker の変数 MAINTENANCE / MAINTENANCE_MESSAGE（worker/maintenance.js）。
import { getMaintenance, maintenanceResponse } from "./maintenance.js";
import { handleReport } from "./report.js";

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const maintenance = getMaintenance(env);
    if (url.pathname === "/api/maintenance") {
      return new Response(JSON.stringify({ state: maintenance.state, message: maintenance.message }), {
        headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" },
      });
    }
    // メンテナンス中は、ほかの API（作者への報告を含む）も 503
    if (maintenance.state === "on" && url.pathname.startsWith("/api/")) return maintenanceResponse(request, maintenance, "Fara");
    // 作者への報告の受け口（worker/report.js。ポケモン・アバター・Bot と同じファイル）
    if (url.pathname === "/api/report" || url.pathname === "/api/report/config") {
      try {
        return await handleReport(request, env, "Fara Portfolio");
      } catch {
        // 詳細（シークレットを含む可能性がある）は返さない
        return new Response(JSON.stringify({ error: "internal_error", message: "サーバーでエラーが発生しました" }), {
          status: 500,
          headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" },
        });
      }
    }
    // /api/* のそのほかは、静的アセットに任せる（SPA の設定で index.html が返る）
    return env.ASSETS.fetch(request);
  },
};

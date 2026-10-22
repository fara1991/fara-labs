// ポートフォリオ（fara-labs.com）の Worker。サイト本体は静的アセットで、/api/* だけがここを通る（wrangler.jsonc の assets.run_worker_first）。
//   GET /api/maintenance → {"state": "off" | "notice" | "on", "message": "..."}（メンテナンスの状態。アプリが起動時に読んで、お知らせやメンテナンス画面を出す）
// メンテナンスの切り替えは、Worker の変数 MAINTENANCE / MAINTENANCE_MESSAGE（worker/maintenance.js）。
import { getMaintenance, maintenanceResponse } from "./maintenance.js";

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const maintenance = getMaintenance(env);
    if (url.pathname === "/api/maintenance") {
      return new Response(JSON.stringify({ state: maintenance.state, message: maintenance.message }), {
        headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" },
      });
    }
    // メンテナンス中は、ほかの API も 503
    if (maintenance.state === "on" && url.pathname.startsWith("/api/")) return maintenanceResponse(request, maintenance, "Fara");
    // /api/* のそのほかは、静的アセットに任せる（SPA の設定で index.html が返る）
    return env.ASSETS.fetch(request);
  },
};

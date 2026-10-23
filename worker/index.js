// ポートフォリオ（fara-labs.com）の Worker。書き出した静的サイト（dist/）を配信する。すべてのリクエストがここを通る（wrangler.jsonc の run_worker_first）。
//   GET /api/report/config・POST /api/report → 作者への報告（external/fara-site-kit/worker/report.js。設定は docs/report-and-maintenance.md）
//   MAINTENANCE=notice … 通常のページの上部（.maintenance-slot）に「更新予定」の帯を入れる（external/fara-site-kit/worker/maintenance.js）
//   MAINTENANCE=true   … すべて 503。ページはメンテナンス画面、/api/ は JSON（external/fara-site-kit/worker/maintenance.js）
// メンテナンスの切り替えは、Worker の変数 MAINTENANCE / MAINTENANCE_MESSAGE（docs/report-and-maintenance.md）。
import { getMaintenance, maintenanceResponse, withNotice } from "../external/fara-site-kit/worker/maintenance.js";
import { handleReport } from "../external/fara-site-kit/worker/report.js";

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const maintenance = getMaintenance(env);
    // メンテナンスの状態（アプリが起動時に読む。kit の MaintenanceInfo）
    if (url.pathname === "/api/maintenance") {
      return new Response(JSON.stringify({ state: maintenance.state, message: maintenance.message }), {
        headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" },
      });
    }
    if (maintenance.state === "on") return maintenanceResponse(request, maintenance, "Fara Portfolio");

    // 作者への報告の受け口（external/fara-site-kit/worker/report.js。ポケモン・アバター・Bot と同じファイル）
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
    if (url.pathname.startsWith("/api/")) return new Response("not found", { status: 404 });

    // ページ・画像などは静的アセット。お知らせ（notice）のときは、HTML の .maintenance-slot に帯を入れる
    return withNotice(await env.ASSETS.fetch(request), maintenance, insertIntoSlot);
  },
};

/** 帯は、ヘッダーとスクロールする本文の間（.maintenance-slot）に置く。ヘッダー・本文と同じく、スクロールしても動かない。 */
function insertIntoSlot(response, html) {
  // eslint-disable-next-line no-undef
  return new HTMLRewriter().on(".maintenance-slot", { element(el) { el.append(html, { html: true }); } }).transform(response);
}

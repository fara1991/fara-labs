// レイアウトの自動計測の設定（kit の tools/layout-check。docs/layout-check.md と CLAUDE.md）。
// 計測の対象は、npm run publish:site が作る publish/wwwroot を配ったもの（tools/layout-check.mjs が配信して、この設定で測る）。
// baseURL は tools/layout-check.mjs が環境変数で渡す（ポートが埋まっていたら別のポートになるため）。直接 cli で測るときは既定の 8840。
const baseURL = process.env.LAYOUT_CHECK_BASE_URL ?? "http://127.0.0.1:8840";

export default {
  baseURL,
  pages: [
    { path: "/", name: "トップ" },
    { path: "/works", name: "作品" },
    { path: "/updates", name: "更新履歴" },
    { path: "/profile", name: "プロフィール" },
    { path: "/links", name: "リンク" },
    { path: "/report", name: "問い合わせ" },
    { path: "/privacy", name: "プライバシーポリシー" },
    // 存在しないパスの画面（NotFoundView）。配信側は /404.html を返すので、Blazor のルーターが、その URL を見つからないと判定して 404 の画面を描く
    { path: "/404.html", name: "404（存在しないパス）" },
  ],
  viewports: [360, 390, 768, 1024, 1440],
  viewportHeight: 800,
  // Blazor が描画を終えた合図（kit の boot-indicator と同じ。MainLayout の .mud-layout）。静的な本文だけの時点では測らない
  readySelector: ".mud-layout",
  // スマホ幅のメニューを Esc で閉じられるか（SiteHeader の開閉ボタン）
  menuEsc: { buttonSelector: "button.menu-toggle" },
  // 既定の数値（600px 未満の押せる範囲は 44px。文字は 12px 以上）
  tapTarget: { minSize: 44, maxWidth: 600 },
  minFontSize: { min: 12, ignoreSelectors: [] },
  focus: { maxStops: 40 },
  timeoutMs: 30000,
  // 既定は同梱の chromium（CI・初回は npx playwright-core install chromium）。ローカルで Edge を使うときは LAYOUT_CHECK_CHANNEL=msedge
  browser: { channel: process.env.LAYOUT_CHECK_CHANNEL || undefined },
  exclusions: [
    {
      rule: "tap-target",
      selector: ".site-nav:not(.is-open) .nav-link",
      reason: "スマホで閉じているメニューの項目（visibility: hidden・pointer-events: none）。見えず押せず、Tab でも止まらないので、当たらないのは当然。計測は読み込んだ直後（閉じた状態）に行うため、開いたメニューの押せる範囲はこの計測では見ない（項目の余白は CSS の .nav-link で 12px）",
    },
    {
      rule: "tap-target",
      selector: "button.mud-button-root[disabled]",
      page: "/report",
      reason: "問い合わせの送信ボタンは、ボット判定（Cloudflare Turnstile）の確認が済むまで無効（disabled・pointer-events: none）。計測の環境（ヘッドレス）では判定が出ないので、無効のまま測ると当たらない。判定の後の押せる範囲は、確認の項目で実機を見る",
    },
  ],
};

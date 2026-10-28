# CLAUDE.md

ポートフォリオサイト（https://fara-labs.com/）。.NET 10 / Blazor WebAssembly + MudBlazor（共通部品は fara-site-kit。`external/fara-site-kit` のサブモジュール）の静的サイトを、Cloudflare Workers（静的アセット + 小さな Worker）に公開する。詳細は README.md、Cloudflare の初期設定は docs/cloudflare-setup.md。

## ブランチ運用（必ず守る）

- 作業ブランチは `develop` から作成する（`master` から切らない）
- 作業ブランチの PR は `develop` 向けに作る
- `master` へは `develop` からのみマージする
- master への push（develop → master の PR のマージ）で Deploy が動き、本番（fara-labs.com）に自動公開される
- master へのマージはユーザーが行う。Claude は develop → master の PR の作成までで、master へはマージしない
- develop への push は Deploy (develop) で確認用（dev.fara-labs.com）に自動公開される

## 構成メモ

- サイトの中身は `src/FaraPortfolio.Web/wwwroot/data/*.json`。文言変更はまずここを見る。ブラウザは起動のときに 1 回だけ読む（`Services/SiteData.cs` の `LoadAsync`。最初の描画の前）。入口ページの本文を作る `tools/PrerenderPages` はファイルから読む（`Load`）
- データの型は `Models/SiteModels.cs`。JSON の項目を増やしたら `tests/FaraPortfolio.Tests/DataFileTests.cs` のチェックも更新する
- ページは `Pages/`（`@page`）、共通の枠は `Layout/MainLayout.razor`（背景・ヘッダー・お知らせの帯・フッターは `Components/AppShell.razor`。ヘッダー・お知らせの帯・フッターは画面に固定し、その間の `.site-scroll`（`.site-main`）だけがスクロールする）。ページの title・description は `Services/PageCatalog.cs`（ページの `PageTitle` と、入口ページの head が同じ値を使う）
- サイトはダーク配色だけ（ライト／ダークの切り替えは無い。`index.html` の `data-bs-theme="dark"` 固定、`MainLayout` の MudBlazor もダーク固定）。localStorage・Cookie は使わない（プライバシーポリシーの記載どおり。sessionStorage にスクロール位置だけ一時保存）
- 共通部品（kit）: フッター（`SiteFooter`）・問い合わせ（`ReportFormView`。`Pages/Report.razor`）・404（`NotFoundView`。`App.razor`）・お知らせの帯（`MaintenanceBanner`）・`SiteLinks`（確認用ホストでのリンク切り替え。開いているホスト名で決める）・`SiteOptions`（値は `Services/SiteInfo.cs`。フッターの SNS は `profile.json` から）。kit は `external/fara-site-kit` を、タグ（いまは v0.7.0。`git submodule status` で確かめられる）に固定。上げるときは kit でタグ → ここでサブモジュールを上げる PR（develop 向け）
- 静的な入口ページ（SEO・JS 無しでも読める）: `dotnet publish` の出力の `index.html`（`<!-- SEO-BODY -->` の区切りつき）をテンプレートに、`tools/prerender-pages.mjs`（ホスト名を `tools/site.json` の origin・devOrigin から渡して、`tools/PrerenderPages` を起動。Razor のページを HtmlRenderer で本文の HTML にして `artifacts/pages.json`）→ `tools/build-pages.mjs`（kit の build-pages。`tools/site.json` の設定。`<path>.html`・`404.html`・`robots.txt`・`sitemap.xml`・`_headers`（CSP。確認用は noindex）を `publish/wwwroot` に作る。og:image の行が見つからなければ失敗する）。公開先（production・develop）と --env の検査は `tools/environments.mjs` に 1 つだけ持つ
- スタイルは `wwwroot/css/app.css` 1 ファイル（色は `:root` の CSS 変数。kit の `fara-site-kit.css` の後に読む）。演出・メニューの外側タップ／Esc・今日の日付は `wwwroot/js/site.js`。更新履歴の絞り込みと NEW、メニューの開閉は Razor（`Pages/Updates.razor`・`Components/UpdateItem.razor`・`Components/SiteHeader.razor`）
  - `.reveal` を付けた要素は画面に入るとフェードイン（`style="--d:N"` で遅延）。`js-ready` が付いたときだけ隠す。ページを移るたびに `MainLayout` が `faraSite.pageChanged` を呼び、新しい本文を監視し直す
  - フェードインは `translate` プロパティで動かしているので、ホバー演出には `transform` を使う
- 公開するのは `publish/wwwroot`（Git に入れない）。Worker（`worker/index.js`）は、報告（`/api/report`）・メンテナンスの状態（`/api/maintenance`）・メンテナンス中の 503 を担当する。お知らせの帯は、アプリが `/api/maintenance` を読んで出す
- 静的ファイルのキャッシュ: `tools/site.json` の `csp.cacheRules` で、`_framework` の指紋付き（`*.wasm`・`dotnet.native.*.js`・`dotnet.runtime.*.js`）だけを `immutable` の長期キャッシュにする（`blazor.webassembly.js`・`dotnet.js` は指紋が無いので、毎回の再検証のまま）。`tools/build-pages.mjs` が、`/data/*` の noindex と共有カード（`twitter:card` = `summary`）も足す
- 公開設定は `wrangler.jsonc`（本番はトップレベル、確認用は `env.develop`）。wrangler のバージョンは `package.json` で固定。`run_worker_first: true`（すべての経路を Worker に通す。`_headers` の CSP は、Worker 経由の静的アセットにも付く）
- `npm run publish:site`（確認用は `publish:site:develop`。`preview` / `check` も）は publish 前に `publish` / `artifacts` / `bin` / `obj` を削除する（`npm run clean`）
- ローカル（WSL）では `dotnet publish` の ILLink が `IL1012` や終了コード 139 でたまに落ちる。`bin` / `obj` を消した状態でも起き、再実行すると通る。コードの問題ではないので、まず再実行する

## 確認

```bash
dotnet test
npm run check     # dotnet publish + 入口ページの作成 + wrangler deploy --dry-run（本番・develop の両方）
npm run preview   # dotnet publish + 入口ページの作成 + wrangler dev（Cloudflare と同じ動き）で http://localhost:8787
npm run test:worker && npm run test:pages   # Worker と入口ページの作成のテスト（kit の worker のテストは test:maintenance / test:report）
npm run lint          # ESLint（tools/・worker/・.github/scripts/・wwwroot/js/。CI の lint job と同じ）
npm run layout-check  # レイアウトの計測（先に npm run publish:site。配るだけで公開はしない。CI の layout-check job と同じ）
```

初回の計測は `npx playwright-core install chromium` が要る（ローカルで Edge を使うなら `LAYOUT_CHECK_CHANNEL=msedge npm run layout-check`）。

## 静的解析とレイアウトの計測（ラチェット）

CI（`build.yml`）の `lint` job と `layout-check` job が、PR・develop・master で動く。どちらも公開はしない。方針は、違反が 0 件のものだけ止める（違反が残るものは warning のまま、件数をここに書く）。

- **ESLint**: 設定は `eslint.config.mjs`（ルールは kit の `tools/lint/eslint.config.shared.mjs`。kit の対象外の `external/**` と生成物は外す）。違反は直す。空の catch には理由のコメントを書く（コメントだけのブロックは通る）。
- **レイアウト計測**: 設定は `layout-check.config.mjs`（ページ・幅 360〜1440px）。`tools/layout-check.mjs` が `publish/wwwroot` を kit の `serve-static.mjs` で配り、kit の CLI で測る。違反は CSS・Razor で直す。
  - 除外は `exclusions` に **`rule`・`selector`・`reason` を必ず書く**（`reason` が無いと設定エラー）。`page`（`/` で始まるパス）で絞れる。理由に、直せない理由（kit の部品・ブラウザの計測の限界など）を書く。
  - いまの除外は 2 件: 閉じたスマホメニューの項目（見えず押せないのが正しい）と、問い合わせの送信ボタン（ボット判定が済むまで disabled）。
  - 計測は読み込み後の一瞬の状態。`scroll-behavior: smooth` を `.site-scroll` に付けると、Tab の移動の途中を測ってしまうので付けない（ページ内ジャンプは `site.js` で滑らかにする）。
- **C#（`Directory.Build.props` と `tools/lint/dotnet.site.editorconfig`）**: 設定は kit の `dotnet.editorconfig` と同じ内容（CA1031 だけ error）。kit のファイルを同時に読むと、同じキーが 2 つのファイルで設定されて両方とも無効になる（`MultipleGlobalAnalyzerKeys`）ので、読まない。kit の設定が変わったら、このファイルも合わせる。
  - error（違反 0 件）: CS4014・VSTHRD100・CA1816・IDE0059・CA1031。動くことは、違反を入れた一時ファイルで確かめた（後で消す）。
  - warning（現在の件数）: CA2000 1（`Program.cs`・HttpClient の寿命）、VSTHRD002 1・VSTHRD003 1・VSTHRD103 3（`SiteData.cs`・`Program.cs` の同期の待ち。起動と `Load` の設計による）。パッケージの既定の VSTHRD200 2（`SiteData.cs`・名前の規則）は、ラチェットの対象外。CA2007（ConfigureAwait）は kit の方針で入れない。
- 新しい違反は CI が止める。warning を error に上げるときは、件数が 0 になってから。

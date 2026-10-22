# CLAUDE.md

ポートフォリオサイト（https://fara-labs.com/）。.NET 10 の静的 SSR（Razor コンポーネントをページごとの HTML に書き出す。ブラウザの .NET 実行環境は使わない）で作り、Cloudflare Workers（静的アセット + 小さな Worker）に公開する。詳細は README.md、Cloudflare の初期設定は docs/cloudflare-setup.md。

## ブランチ運用（必ず守る）

- 作業ブランチは `develop` から作成する（`master` から切らない）
- 作業ブランチの PR は `develop` 向けに作る
- `master` へは `develop` からのみマージする
- master への push（develop → master の PR のマージ）で Deploy が動き、本番（fara-labs.com）に自動公開される
- master へのマージはユーザーが行う。Claude は develop → master の PR の作成までで、master へはマージしない
- develop への push は Deploy (develop) で確認用（dev.fara-labs.com）に自動公開される

## 構成メモ

- サイトの中身は `src/FaraPortfolio.Web/wwwroot/data/*.json`。文言変更はまずここを見る。JSON は書き出しのとき HTML に埋め込まれる（ブラウザは読まない）
- データの型は `Models/SiteModels.cs`、読み込みは `Services/SiteData.cs`（書き出し・起動のときに 1 回だけ。壊れていれば失敗する）
- ページは `Pages/`（`@page`）、共通の枠は `Layout/MainLayout.razor`（ヘッダー・お知らせの帯は固定し、`.site-main` だけを差し替える想定）、ページごとの title・OGP は `Components/PageMeta.razor`
- 書き出しは `Publishing/StaticSiteExporter.cs`（`dotnet run --project src/FaraPortfolio.Web -c Release --no-launch-profile -- --export dist`。相対パスはリポジトリのルートから）。確認用は `--site-host dev.fara-labs.com`
- スタイルは `wwwroot/css/app.css` 1 ファイル（色は `:root` の CSS 変数）。演出・メニュー・更新履歴の絞り込みは `wwwroot/js/site.js`、問い合わせフォームは `wwwroot/js/report.js`
  - `.reveal` を付けた要素は画面に入るとフェードイン（`style="--d:N"` で遅延）。`js-ready` が付いたときだけ隠す（JS が無ければ本文は見える）
  - フェードインは `translate` プロパティで動かしているので、ホバー演出には `transform` を使う
- JSON の項目を増やしたら `tests/FaraPortfolio.Tests/DataFileTests.cs` のチェックも更新する
- 書き出しの HTML は `dist/`（Git に入れない）。Worker（`worker/index.js`）は `dist/` を配信し、お知らせの帯（`.maintenance-slot`）とメンテナンス中の 503 を担当する

- 公開設定は `wrangler.jsonc`（本番はトップレベル、確認用は `env.develop`）。wrangler のバージョンは `package.json` で固定。`run_worker_first: true`（すべての経路を Worker に通す）
- `npm run publish:site`（`preview` / `check` も）は書き出し前に `dist` / `bin` / `obj` を削除する（`npm run clean`）

## 確認

```bash
dotnet test
npm run check     # 書き出し + wrangler deploy --dry-run（本番・develop の両方）
npm run preview   # 書き出し + wrangler dev（Cloudflare と同じ動き）で http://localhost:8787
node --test worker/index.test.mjs tests/js/report.test.cjs   # Worker と問い合わせフォームのテスト
```

# CLAUDE.md

ポートフォリオサイト（https://app-fara.com/）。.NET 8 / Blazor WebAssembly の静的サイトを Cloudflare Workers（静的アセットのみ）に公開する。詳細は README.md、Cloudflare の初期設定は docs/cloudflare-setup.md。

## ブランチ運用（必ず守る）

- 作業ブランチは `develop` から作成する（`master` から切らない）
- 作業ブランチの PR は `develop` 向けに作る
- `master` へは `develop` からのみマージする
- 本番（app-fara.com）の公開は Actions の Deploy ワークフロー（workflow_dispatch・master 固定）を手動実行のみ。master への push / マージで本番に自動公開する仕組みは追加しない
- develop への push は Deploy (develop) で確認用（dev.app-fara.com）に自動公開される

## 構成メモ

- サイトの中身は `src/FaraPortfolio.Web/wwwroot/data/*.json`。文言変更はまずここを見る
- データの型は `Models/SiteModels.cs`、読み込みは `Services/SiteData.cs`（起動時に 1 回だけ）
- スタイルは `wwwroot/css/app.css` 1 ファイル（色は `:root` の CSS 変数）。スクロール演出は `wwwroot/js/site.js`
  - `.reveal` を付けた要素は画面に入るとフェードイン（`style="--d:N"` で遅延）
  - フェードインは `translate` プロパティで動かしているので、ホバー演出には `transform` を使う
- JSON の項目を増やしたら `tests/FaraPortfolio.Tests/DataFileTests.cs` のチェックも更新する

- 公開設定は `wrangler.jsonc`（本番はトップレベル、確認用は `env.develop`）。wrangler のバージョンは `package.json` で固定
- `npm run publish:site`（`preview` / `check` も）は publish 前に `bin` / `obj` / `publish` を削除する（`npm run clean`）
- ローカル（WSL）では `dotnet publish` の ILLink が `IL1012` や終了コード 139 でたまに落ちる。`bin` / `obj` を消した状態でも起き、再実行すると通る。コードの問題ではないので、まず再実行する

## 確認

```bash
dotnet test
npm run check     # dotnet publish + wrangler deploy --dry-run（本番・develop の両方）
npm run preview   # Cloudflare と同じ動きで http://localhost:8787
```

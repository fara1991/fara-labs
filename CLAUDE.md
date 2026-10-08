# CLAUDE.md

ポートフォリオサイト（https://app-fara.com/）。.NET 8 / Blazor WebAssembly の静的サイトを GitHub Pages に公開する。詳細は README.md。

## ブランチ運用（必ず守る）

- 作業ブランチは `develop` から作成する（`master` から切らない）
- 作業ブランチの PR は `develop` 向けに作る
- `master` へは `develop` からのみマージする
- 公開は Actions の Deploy ワークフロー（workflow_dispatch・master 固定）を手動実行。push / マージで自動公開する仕組みは追加しない

## 構成メモ

- サイトの中身は `src/FaraPortfolio.Web/wwwroot/data/*.json`。文言変更はまずここを見る
- データの型は `Models/SiteModels.cs`、読み込みは `Services/SiteData.cs`（起動時に 1 回だけ）
- スタイルは `wwwroot/css/app.css` 1 ファイル（色は `:root` の CSS 変数）。スクロール演出は `wwwroot/js/site.js`
  - `.reveal` を付けた要素は画面に入るとフェードイン（`style="--d:N"` で遅延）
  - フェードインは `translate` プロパティで動かしているので、ホバー演出には `transform` を使う
- JSON の項目を増やしたら `tests/FaraPortfolio.Tests/DataFileTests.cs` のチェックも更新する

## 確認

```bash
dotnet test
dotnet publish src/FaraPortfolio.Web -c Release -o publish
```

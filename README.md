# fara-portfolio-site

自分用のポートフォリオサイト（https://app-fara.com/）。C# / Blazor WebAssembly 製の静的サイトで、GitHub Pages に公開します。

- 公開中の作品（ポケモン対戦アシスタント、Beat Saber Mod など）の紹介
- 更新履歴（何月何日に何を更新したか）
- SNS リンク

サーバー・データベースは使っていません。サイトの中身は `wwwroot/data/` の JSON を編集して更新します。

## フォルダ構成

```
fara-portfolio-site/
├── FaraPortfolio.sln
├── global.json                       使用する .NET SDK（8.0）
├── src/FaraPortfolio.Web/            Blazor WebAssembly 本体
│   ├── Components/                   ヘッダー・フッター・アイコン・カードなどの部品
│   ├── Layout/MainLayout.razor       全ページ共通の枠（パララックス背景）
│   ├── Pages/                        Home / Works / Updates / Links
│   ├── Models/ Services/             データの型と読み込み
│   └── wwwroot/
│       ├── data/                     ★ サイトの中身（ここを編集する）
│       │   ├── profile.json          名前・自己紹介・SNS リンク
│       │   ├── works.json            作品一覧
│       │   └── updates.json          更新履歴
│       ├── css/app.css               デザイン（紫ベース）
│       └── js/site.js                スクロール演出（パララックス・フェードイン）
├── tests/FaraPortfolio.Tests/        data/*.json の書式チェック
└── .github/workflows/
    ├── build.yml                     PR・push 時にビルドとテストのみ（公開しない）
    └── deploy.yml                    手動実行で master を公開
```

## サイトの更新

### 更新履歴を追加する

`src/FaraPortfolio.Web/wwwroot/data/updates.json` に 1 件追加します（並び順は自由。サイト側で新しい順に並びます）。

```jsonc
{
  "date": "2026-11-01",
  "category": "work",          // site（サイト） / work（作品） / news（お知らせ）
  "title": "ダメージ計算サイトに素早さ比較を追加",
  "body": "本文（任意）",
  "link": "https://pokemon.app-fara.com/",  // 任意。"/works" のようなサイト内パスも可
  "linkLabel": "サイトを開く"               // 任意
}
```

更新から 14 日以内のものには自動で「NEW」が付きます。

### 作品を追加する

`works.json` に 1 件追加します。`icon` は `Components/Icon.razor` にある名前（`pokeball` / `saber` / `code` / `sparkles` など）、`accent` は `violet` / `pink` / `indigo` / `cyan` から選びます。

### SNS を追加する

`profile.json` の `socials` の `url` を埋めると表示されます（空欄のものは非表示）。

JSON に書き間違いがあると `dotnet test` が失敗するので、公開前に気付けます。

## 公開（デプロイ）

PR をマージしても自動では公開されません。公開したいタイミングで手動実行します。

1. GitHub の **Actions** タブ → **Deploy** → **Run workflow**
2. 常に **master ブランチ** の内容がビルド・テストされ、問題なければ公開されます

### 初回だけ必要な設定

1. リポジトリの **Settings → Pages → Build and deployment → Source** を **GitHub Actions** にする
2. 同じ画面の **Custom domain** に `app-fara.com` を入力して保存し、DNS の確認後に **Enforce HTTPS** を有効にする
3. Cloudflare の DNS に以下を追加（Proxy status は **DNS only（グレーの雲）**）
   | Type | Name | Content |
   |---|---|---|
   | CNAME | `@`（app-fara.com） | `fara1991.github.io` |
   | CNAME | `www` | `fara1991.github.io` |

   ※ ルートドメインの CNAME は Cloudflare が自動で A レコード相当に変換（CNAME Flattening）します。`pokemon.app-fara.com` の設定はそのままで大丈夫です。

## ブランチ運用

```
feature/xxx ──PR──▶ develop ──PR──▶ master ──(手動 Deploy)──▶ app-fara.com
```

- 作業ブランチは **develop から** 作成する（`feature/xxx` など）
- 作業ブランチは **develop へ** マージする
- リリースするときは **develop → master** へマージし、Actions の Deploy を手動実行する

## ローカルで動かす

```bash
dotnet run --project src/FaraPortfolio.Web   # http://localhost:5072
dotnet test                                  # data/*.json のチェック
```

# fara-labs

自分用のポートフォリオサイト（https://fara-labs.com/）。C# / .NET 10 の Blazor WebAssembly + MudBlazor（共通部品は fara-site-kit）で作った静的サイトを、Cloudflare Workers に公開します。

- プロフィール
- 作ったもの（ポケモン対戦アシスタントなど）の紹介
- 更新履歴（何月何日に何を更新したか）
- SNS リンク

サーバー・データベースは使っていません（Worker は、メンテナンスの状態・問い合わせの受け口だけ）。サイトの中身は `wwwroot/data/` の JSON を編集して更新します。ブラウザは起動のときに JSON を 1 回だけ読み、検索エンジン・JavaScript の無い環境向けの本文入りの HTML は、公開用の作成（`npm run publish:site`）のときに JSON から作ります。

## フォルダ構成

```
fara-labs/
├── FaraPortfolio.sln
├── global.json                       使用する .NET SDK（10.0）
├── wrangler.jsonc                    Cloudflare Workers の公開設定（本番 / develop）
├── package.json                      wrangler のバージョン固定と、公開用の作成・テストのスクリプト
├── external/fara-site-kit/           共通部品（サブモジュール。Razor・Core・Worker の JS・build-pages・CSP）
├── docs/cloudflare-setup.md          Cloudflare・GitHub の初期設定手順
├── src/FaraPortfolio.Web/            サイト本体（Blazor WebAssembly）
│   ├── App.razor                     ルーティング（見つからない URL は kit の 404）
│   ├── Components/                   AppShell（背景・ヘッダー・フッターの枠）・ヘッダー・アイコン・カード
│   ├── Layout/MainLayout.razor       全ページ共通の枠（MudBlazor の土台・お知らせの帯）
│   ├── Pages/                        Home / Profile / Works / Updates / Links / Report / Privacy
│   ├── Models/ Services/             データの型と読み込み・ページの題名（PageCatalog）・サイト設定（SiteInfo）
│   └── wwwroot/
│       ├── index.html                起動の土台（読み込み表示・CSS/JS の読み込み順。ダーク固定）
│       ├── data/                     ★ サイトの中身（ここを編集する）
│       │   ├── profile.json          名前・自己紹介・SNS リンク
│       │   ├── works.json            作品一覧
│       │   └── updates.json          更新履歴
│       ├── css/app.css               デザイン（紫ベース）
│       ├── js/site.js                スクロール演出・メニューの外側タップ／Esc・今日の日付
│       └── .assetsignore             Cloudflare にアップロードしないファイル
├── tools/                            公開用の作成: PrerenderPages（本文の HTML を作る）・build-pages.mjs（kit の build-pages）・site.json
├── tests/FaraPortfolio.Tests/        data/*.json の書式チェック、入口ページの本文の検証（PrerenderTests）、リンク・SNS・NEW のテスト
├── worker/                           Worker（メンテナンスの状態・問い合わせの受け口）と、そのテスト
├── publish/                          公開する静的サイト（Git に入れない。publish/wwwroot が公開する中身）
└── .github/workflows/
    ├── build.yml                     PR・master への push でビルド・テスト・公開設定チェック（公開しない）
    ├── deploy-develop.yml            develop への push で確認用サイト（dev.fara-labs.com）に公開
    └── deploy.yml                    master への push で本番（fara-labs.com）に公開
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
  "link": "https://pokemon.fara-labs.com/",  // 任意。"/works" のようなサイト内パスも可
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

| 公開先 | タイミング |
|---|---|
| https://dev.fara-labs.com/（確認用） | develop に push（PR のマージ含む）されるたびに自動 |
| https://fara-labs.com/（本番） | master に push（develop → master の PR のマージ含む）されるたびに自動。手動で公開し直すときは **Actions → Deploy (production) → Run workflow** |

どちらもテストが通らなければ公開されません。初回の設定（API トークンの発行・GitHub への登録・DNS）は [docs/cloudflare-setup.md](docs/cloudflare-setup.md) を参照してください。

## ブランチ運用

```
feature/xxx ──PR──▶ develop ──PR──▶ master ──(自動)──▶ fara-labs.com
                       │
                       └──(自動)──▶ dev.fara-labs.com
```

- 作業ブランチは **develop から** 作成する（`feature/xxx` など）
- 作業ブランチは **develop へ** マージする
- リリースするときは **develop → master** へマージする（マージすると本番に公開される）

## ローカルで動かす

```bash
git submodule update --init                  # 初回のみ（external/fara-site-kit）
dotnet run --project src/FaraPortfolio.Web   # http://localhost:5072（画面の確認。メンテナンスの状態・問い合わせの送信は Worker が無いので動かない）
dotnet test                                  # data/*.json と、入口ページの本文のチェック

# Cloudflare と同じ動き（/works などの直接アクセス含む）で確認したいとき（Node.js が必要）
npm ci                                       # 初回のみ
npm run preview                              # dotnet publish → 入口ページの作成 → wrangler dev → http://localhost:8787（Worker も動く）

# 公開する静的サイトを作るだけ（publish/wwwroot に出る。確認用のリンクにするときは publish:site:develop）
npm run publish:site
```

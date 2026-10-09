# fara-portfolio-site

自分用のポートフォリオサイト（https://app-fara.com/）。C# / Blazor WebAssembly 製の静的サイトで、Cloudflare Workers に公開します。

- 公開中の作品（ポケモン対戦アシスタント、Beat Saber Mod など）の紹介
- 更新履歴（何月何日に何を更新したか）
- SNS リンク

サーバー・データベースは使っていません。サイトの中身は `wwwroot/data/` の JSON を編集して更新します。

## フォルダ構成

```
fara-portfolio-site/
├── FaraPortfolio.sln
├── global.json                       使用する .NET SDK（8.0）
├── wrangler.jsonc                    Cloudflare Workers の公開設定（本番 / develop）
├── package.json                      wrangler（Cloudflare の CLI）のバージョン固定
├── docs/cloudflare-setup.md          Cloudflare・GitHub の初期設定手順
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
│       ├── js/site.js                スクロール演出（パララックス・フェードイン）
│       └── .assetsignore             Cloudflare にアップロードしないファイル
├── tests/FaraPortfolio.Tests/        data/*.json の書式チェック
└── .github/workflows/
    ├── build.yml                     PR・master への push でビルド・テスト・公開設定チェック（公開しない）
    ├── deploy-develop.yml            develop への push で確認用サイト（dev.app-fara.com）に公開
    └── deploy.yml                    手動実行で master を本番（app-fara.com）に公開
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

| 公開先 | タイミング |
|---|---|
| https://dev.app-fara.com/（確認用） | develop に push（PR のマージ含む）されるたびに自動 |
| https://app-fara.com/（本番） | **Actions → Deploy → Run workflow** を手動実行したときだけ。常に master の内容 |

どちらもテストが通らなければ公開されません。初回の設定（API トークンの発行・GitHub への登録・DNS）は [docs/cloudflare-setup.md](docs/cloudflare-setup.md) を参照してください。

## ブランチ運用

```
feature/xxx ──PR──▶ develop ──PR──▶ master ──(手動 Deploy)──▶ app-fara.com
                       │
                       └──(自動)──▶ dev.app-fara.com
```

- 作業ブランチは **develop から** 作成する（`feature/xxx` など）
- 作業ブランチは **develop へ** マージする
- リリースするときは **develop → master** へマージし、Actions の Deploy を手動実行する

## ローカルで動かす

```bash
dotnet run --project src/FaraPortfolio.Web   # http://localhost:5072（普段の確認はこれ）
dotnet test                                  # data/*.json のチェック

# Cloudflare と同じ動き（/works などの直接アクセス含む）で確認したいとき（Node.js が必要）
npm ci                                       # 初回のみ
npm run preview                              # http://localhost:8787
```

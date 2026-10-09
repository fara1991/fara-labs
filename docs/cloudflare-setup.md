# Cloudflare Workers 公開の初期設定

このサイトは Cloudflare Workers（静的アセットのみ）で公開します。最初に 1 回だけ、以下の設定が必要です。
費用はかかりません（API トークンは無料。無料プランのまま無料枠を超えても課金されず、超えた分がエラーになるだけです）。

| 公開先 | Worker 名 | 公開のタイミング | ワークフロー |
|---|---|---|---|
| https://app-fara.com/ | `fara-portfolio` | Actions から手動実行（master） | `.github/workflows/deploy.yml` |
| https://dev.app-fara.com/ | `fara-portfolio-develop` | develop への push ごと | `.github/workflows/deploy-develop.yml` |

設定の順番: **1 → 2 → 3 → 4 → 5**。3 と 4 が終わる前に develop へマージすると Deploy (develop) が失敗します（失敗するだけで害はありません。設定後に Actions から再実行できます）。

---

## 1. DNS の事前確認

Cloudflare ダッシュボード → `app-fara.com` → **DNS → Records** で、次の名前のレコードが **無い** ことを確認します。
Worker のカスタムドメインは初回デプロイ時に自動でレコードを作るため、同じ名前の A / AAAA / CNAME があると失敗します。

- `app-fara.com`（Name が `@`）
- `dev.app-fara.com`（Name が `dev`）

> `pokemon.app-fara.com`（GitHub Pages）のレコードは触りません。そのままで大丈夫です。

## 2. API トークンを発行する

1. Cloudflare ダッシュボード右上のアイコン → **My Profile** → **API Tokens** → **Create Token**
2. テンプレート **Edit Cloudflare Workers** の **Use template**
3. 権限（Permissions）はテンプレートのままで OK。範囲を絞ります:
   - **Account Resources**: `Include` → 自分のアカウント
   - **Zone Resources**: `Include` → `Specific zone` → `app-fara.com`
   - **TTL**（任意）: 有効期限を付けたい場合は終了日を設定（期限が来たら 2. をやり直して 3. のシークレットを更新）
4. **Continue to summary** → **Create Token**
5. 表示されたトークンをコピー（**この画面でしか表示されません**。閉じてしまったら作り直し）

あわせて **アカウント ID** も控えます: ダッシュボードのアカウントのホーム、または **Workers & Pages** の画面右側にある **Account ID** をコピー。

## 3. GitHub にシークレットを登録する

リポジトリの **Settings → Secrets and variables → Actions**

| タブ | 名前 | 値 |
|---|---|---|
| **Secrets** → New repository secret | `CLOUDFLARE_API_TOKEN` | 2. で発行したトークン |
| **Variables** → New repository variable | `CLOUDFLARE_ACCOUNT_ID` | 2. で控えたアカウント ID |

アカウント ID は秘密情報ではないので Variables に置いています。トークンは Secrets なので、登録後は GitHub 上でも中身を見られません（ログにも `***` で伏せられます）。

## 4. GitHub の Environments を作る

本番に公開できるブランチを master に限定するための設定です。

**Settings → Environments → New environment**

| 名前 | Deployment branches and tags | 任意の追加設定 |
|---|---|---|
| `production` | **Selected branches and tags** → `master` を追加 | **Required reviewers** に自分を入れると、公開前に承認ボタンを押す 1 ステップが入る |
| `develop` | **Selected branches and tags** → `develop` を追加 | なし |

## 5. 初回の公開

1. 作業ブランチ → **develop** に PR をマージ
   → Deploy (develop) が自動で動き、https://dev.app-fara.com/ に公開される（初回はカスタムドメインの作成で数分かかることがあります）
2. dev.app-fara.com で表示を確認
3. **develop → master** に PR をマージ（この時点ではまだ公開されない）
4. **Actions → Deploy → Run workflow**（Use workflow from: `master`）
   → https://app-fara.com/ に公開

---

## 任意の設定

### www.app-fara.com を app-fara.com に転送する

1. **DNS → Records → Add record**: Type `AAAA` / Name `www` / IPv6 `100::` / Proxy status **Proxied（オレンジの雲）**
2. **Rules → Redirect Rules → Create rule → Templates** の **Redirect from WWW to root** を選んで保存

### 確認用サイトを自分だけに見せる（Cloudflare Access・50 人まで無料）

dev.app-fara.com は初期状態だと URL を知っていれば誰でも見られます（検索エンジンには載らないようにしてあります）。
自分だけにしたい場合: **Zero Trust → Access → Applications → Add an application → Self-hosted** で
ドメイン `dev.app-fara.com`、ポリシーを「自分のメールアドレスを Include」にします。開くとメールに届くコードでログインする形になります。

### うまくいかないとき

| 症状 | 対処 |
|---|---|
| `Authentication error [code: 10000]` | トークンの貼り間違い・期限切れ。2. → 3. をやり直す |
| カスタムドメインの作成で失敗（already exists / conflict） | 1. の DNS レコードが残っている。削除してから再実行 |
| カスタムドメインの作成で権限エラー | ダッシュボードの **Workers & Pages → 該当 Worker → Settings → Domains & Routes → Add → Custom domain** から 1 回だけ手動で追加し、再実行 |
| Deploy が「master から実行してください」で失敗 | Run workflow のときに **Use workflow from** を `master` にする |

## ローカルで Cloudflare と同じ動きを確認する

```bash
npm ci            # 初回のみ（wrangler をインストール）
npm run preview   # dotnet publish → wrangler dev（http://localhost:8787）
npm run check     # 公開設定の検証（Cloudflare にはつながない）
```

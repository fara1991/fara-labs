# Cloudflare Workers 公開の初期設定（fara-labs.com）

このサイトは Cloudflare Workers（静的アセットのみ）で公開します。ドメイン `fara-labs.com` の DNS は Cloudflare で管理し、
ポケモンツール（pokemon.fara-labs.com）は別リポジトリから別の Worker として公開しています。最初に 1 回だけ、以下の設定が必要です。
費用はドメイン代のみです（API トークンは無料。無料プランのまま無料枠を超えても課金されず、超えた分がエラーになるだけです）。

| 公開先 | 置き場所 | 公開のタイミング | ワークフロー |
|---|---|---|---|
| https://fara-labs.com/ | Worker `fara-portfolio` | master への push ごと | `.github/workflows/deploy.yml` |
| https://dev.fara-labs.com/ | Worker `fara-portfolio-dev` | develop への push ごと | `.github/workflows/deploy-develop.yml` |
| https://pokemon.fara-labs.com/ | Worker `fara-pokemon`（`fara-pokemon-battle-assistant` リポジトリ） | そのリポジトリの master への push ごと | そのリポジトリの `.github/workflows/deploy.yml` |
| https://pokemon-dev.fara-labs.com/ | Worker `fara-pokemon-dev`（同上） | そのリポジトリの develop への push ごと | そのリポジトリの `.github/workflows/deploy-develop.yml` |
| https://avatar.fara-labs.com/ | Worker `fara-avatar`（`fara-avatar-modding-guide` リポジトリ） | そのリポジトリの master への push ごと | そのリポジトリの `.github/workflows/deploy.yml` |
| https://avatar-dev.fara-labs.com/ | Worker `fara-avatar-dev`（同上） | そのリポジトリの develop への push ごと | 同上 |

設定の順番: **1 → 2 → 3 → 4 → 5 → 6 → 7**。4 が終わる前に develop へマージすると Deploy (develop) が失敗します（失敗するだけで害はありません。設定後に Actions から再実行できます）。

---

## 1. fara-labs.com を Cloudflare に追加する

- **Cloudflare Registrar で取得した場合**: ダッシュボードのアカウントのホームに `fara-labs.com` が最初から並んでいるので、何もしなくて OK。
- **他社で取得した場合**: アカウントのホーム → **Add a domain** → `fara-labs.com` を入力 → プランは **Free**。
  表示された 2 つのネームサーバー（`xxx.ns.cloudflare.com`）を、ドメインを取得した会社の管理画面で設定します。
  ゾーンの画面に **Active** と出たら次へ（数分〜最大 24 時間）。

続けて、そのゾーンの **SSL/TLS → Overview** を **Full (strict)** にします（初期値の Full のままでも動きます）。

## 2. DNS の事前確認

Cloudflare ダッシュボード → `fara-labs.com` → **DNS → Records** で、次の名前のレコードが **無い** ことを確認します。
Worker のカスタムドメインは初回デプロイ時に自動でレコードを作るため、同じ名前の A / AAAA / CNAME があると失敗します。

- `fara-labs.com`（Name が `@`）
- `dev.fara-labs.com`（Name が `dev`）

> 他社から移したときに、取得元の「駐車ページ」用の A レコードなどが自動で取り込まれていることがあります。あれば削除します。
> `pokemon` も 6. で Worker が自動で作るので、レコードは不要です。

## 3. API トークンを発行する

1. Cloudflare ダッシュボード右上のアイコン → **My Profile** → **API Tokens** → **Create Token**
2. テンプレート **Edit Cloudflare Workers** の **Use template**
3. 権限（Permissions）はテンプレートのままで OK。範囲を絞ります:
   - **Account Resources**: `Include` → 自分のアカウント
   - **Zone Resources**: `Include` → `Specific zone` → `fara-labs.com`
   - **TTL**（任意）: 有効期限を付けたい場合は終了日を設定（期限が来たら 3. をやり直して 4. のシークレットを更新）
4. **Continue to summary** → **Create Token**
5. 表示されたトークンをコピー（**この画面でしか表示されません**。閉じてしまったら作り直し）

あわせて **アカウント ID** も控えます: ダッシュボードのアカウントのホーム、または **Workers & Pages** の画面右側にある **Account ID** をコピー。

## 4. GitHub にシークレットを登録する

リポジトリの **Settings → Secrets and variables → Actions**

| タブ | 名前 | 値 |
|---|---|---|
| **Secrets** → New repository secret | `CLOUDFLARE_API_TOKEN` | 3. で発行したトークン |
| **Variables** → New repository variable | `CLOUDFLARE_ACCOUNT_ID` | 3. で控えたアカウント ID |

アカウント ID は秘密情報ではないので Variables に置いています。トークンは Secrets なので、登録後は GitHub 上でも中身を見られません（ログにも `***` で伏せられます）。

## 5. GitHub の Environments を作る

本番に公開できるブランチを master に限定するための設定です。

**Settings → Environments → New environment**

| 名前 | Deployment branches and tags | 任意の追加設定 |
|---|---|---|
| `production` | **Selected branches and tags** → `master` を追加 | **Required reviewers** に自分を入れると、公開前に承認ボタンを押す 1 ステップが入る |
| `develop` | **Selected branches and tags** → `develop` を追加 | なし |

## 6. pokemon.fara-labs.com（ポケモンツール）

ポケモンツールは `fara1991/fara-pokemon-assistance` リポジトリから、Worker `fara-pokemon` として同じ Cloudflare アカウントに公開します
（設定はそのリポジトリの `wrangler.jsonc` と `.github/workflows/deploy.yml`）。

1. `fara-pokemon-assistance` の **Settings → Secrets and variables → Actions** に、4. と同じ `CLOUDFLARE_API_TOKEN` / `CLOUDFLARE_ACCOUNT_ID` を登録
   （トークンは 3. のものをそのまま使えます。範囲が fara-labs.com のゾーンなので pokemon.fara-labs.com も含まれます）
2. DNS に `pokemon`・`pokemon-dev` のレコードが **無い** ことを確認（GitHub Pages 時代の CNAME が残っていれば削除）
3. そのリポジトリの master に push（PR のマージ含む）すると公開される。https://pokemon.fara-labs.com/ が開けることを確認
4. GitHub Pages を使っていた場合は、そのリポジトリの **Settings → Pages** で公開を停止

## 7. 初回の公開

1. 作業ブランチ → **develop** に PR をマージ
   → Deploy (develop) が自動で動き、https://dev.fara-labs.com/ に公開される（初回はカスタムドメインの作成で数分かかることがあります）
2. dev.fara-labs.com で表示を確認
3. **develop → master** に PR をマージ
   → Deploy が自動で動き、https://fara-labs.com/ に公開される

---

## 任意の設定

### www.fara-labs.com を fara-labs.com に転送する

1. **DNS → Records → Add record**: Type `AAAA` / Name `www` / IPv6 `100::` / Proxy status **Proxied（オレンジの雲）**
2. **Rules → Redirect Rules → Create rule → Templates** の **Redirect from WWW to root** を選んで保存

### 確認用サイトを自分だけに見せる（Cloudflare Access・50 人まで無料）

確認用サイト（dev.fara-labs.com・pokemon-dev.fara-labs.com）は、初期状態だと URL を知っていれば誰でも見られます（検索エンジンには載らないようにしてあります）。
Cloudflare Access を前に置くと、許可したメールアドレスに届くコードでログインした人だけが見られるようになります。

1. ダッシュボードの左メニュー **Zero Trust**（初回はチーム名の入力と **Free** プランの選択。支払い情報の登録を求められても請求はされません）
2. **Access → Applications → Add an application → Self-hosted**
   - **Application name**: `dev sites` など
   - **Session Duration**: `24 hours` など（ログインし直すまでの期間）
   - **Add public hostname** で 2 つ追加: Domain `fara-labs.com`、Subdomain `dev` と `pokemon-dev`
3. **Policies → Add a policy**
   - **Policy name**: `me` など / **Action**: `Allow`
   - **Include** → Selector `Emails` → 自分のメールアドレス
4. **Login methods** は **One-time PIN**（初期状態で有効）のままで OK → 保存
5. シークレットウィンドウで https://pokemon-dev.fara-labs.com/ を開き、Cloudflare のログイン画面が出ること、メールのコードで入れることを確認

Worker は `workers_dev` / `preview_urls` を無効にしてあるので、`*.workers.dev` から Access を迂回して見られることはありません。
確認用サイトを増やしたときは、このアプリケーションに public hostname を追加します。

#### ログイン画面の見た目を変える

**Zero Trust → 再利用可能なコンポーネント → カスタムページ → ログイン ページにアクセス → 管理**

| 項目 | 値 |
|---|---|
| 組織名 | `Fara Labs` |
| ロゴ | `https://fara-labs.com/images/logo.png`（旧ファビコンの「F」の 256px PNG（ファビコンは分子モデルに変更済み。ロゴは当面そのまま）。本番に公開されてから設定する） |
| 背景色 | `#0c0616`（サイトの背景色） |
| ヘッダーテキスト | `Fara Labs 確認用サイト` |
| フッターテキスト | `関係者のみ閲覧できます。登録済みのメールアドレスに届くコードでログインしてください。` |

- ログイン画面の「Log in to ○○」はアプリケーション名が出るので、アプリケーション名も `Fara Labs（確認用）` などにしておく
- アプリケーションの **認証 → インスタント認証を適用** をオンにすると、「Sign in with」のボタンを押す画面を飛ばしてメールアドレスの入力から始まる
- チーム名（**設定 → チーム ドメイン**）は `fara-labs`（ログイン画面の URL が `fara-labs.cloudflareaccess.com` になる）

### 作者への報告・メンテナンス画面

3 つのサイトの「作者へ報告」（Discord に通知）と、メンテナンス画面（Cloudflare の WAF ルール）の設定は、[report-and-maintenance.md](report-and-maintenance.md) にまとめています。

### うまくいかないとき

| 症状 | 対処 |
|---|---|
| `Authentication error [code: 10000]` | トークンの貼り間違い・期限切れ、または fara-labs.com がトークンの範囲に入っていない。3. → 4. をやり直す |
| カスタムドメインの作成で失敗（already exists / conflict） | 2. の DNS レコードが残っている。削除してから再実行 |
| カスタムドメインの作成で権限エラー | ダッシュボードの **Workers & Pages → 該当 Worker → Settings → Domains & Routes → Add → Custom domain** から 1 回だけ手動で追加し、再実行 |
| Deploy を手動実行したら「master から実行してください」で失敗 | Run workflow のときに **Use workflow from** を `master` にする |

## ローカルで Cloudflare と同じ動きを確認する

```bash
npm ci            # 初回のみ（wrangler をインストール）
npm run preview   # dotnet publish → 入口ページの作成（publish/wwwroot）→ wrangler dev（http://localhost:8787）
npm run check     # 公開設定の検証（Cloudflare にはつながない）
```

# Cloudflare Workers 公開の初期設定（fara-labs.com）

このサイトは Cloudflare Workers（静的アセットのみ）で公開します。ドメイン `fara-labs.com` の DNS は Cloudflare で管理し、
ポケモンツール（pokemon.fara-labs.com）は別リポジトリから別の Worker として公開しています。最初に 1 回だけ、以下の設定が必要です。
費用はドメイン代のみです（API トークンは無料。無料プランのまま無料枠を超えても課金されず、超えた分がエラーになるだけです）。

| 公開先 | 置き場所 | 公開のタイミング | ワークフロー |
|---|---|---|---|
| https://fara-labs.com/ | Worker `fara-portfolio` | Actions から手動実行（master） | `.github/workflows/deploy.yml` |
| https://dev.fara-labs.com/ | Worker `fara-portfolio-develop` | develop への push ごと | `.github/workflows/deploy-develop.yml` |
| https://pokemon.fara-labs.com/ | Worker `fara-pokemon`（`fara-pokemon-assistance` リポジトリ） | そのリポジトリの master への push ごと | そのリポジトリの `.github/workflows/deploy.yml` |
| https://pokemon-dev.fara-labs.com/ | Worker `fara-pokemon-develop`（同上） | そのリポジトリの develop への push ごと | そのリポジトリの `.github/workflows/deploy-develop.yml` |

設定の順番: **1 → 2 → 3 → 4 → 5 → 6 → 7**。4 が終わる前に develop へマージすると Deploy (develop) が失敗します（失敗するだけで害はありません。設定後に Actions から再実行できます）。

> **app-fara.com から移行する場合**（2026-10 に移行）: 4（シークレット）と 5（Environments）は設定済みなのでそのままで OK。
> **1 → 2 → 3（既存トークンの編集）→ 6 → 7**、最後に「[旧ドメイン app-fara.com から転送する](#旧ドメイン-app-faracom-から-fara-labscom-に転送する)」を行います。

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

**app-fara.com から移行する場合**は、既存のトークンを編集するだけで済みます（トークンの値は変わらないので 4. はやり直し不要）:
**My Profile → API Tokens** → 使っているトークンの **…** → **Edit** → **Zone Resources** に `Include` → `Specific zone` → `fara-labs.com` を追加 → **Continue to summary** → **Update Token**。
（app-fara.com の行は、下の「旧ドメインから転送する」が終わるまで残しておきます。）

新しく作る場合:

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
3. **develop → master** に PR をマージ（この時点ではまだ公開されない）
4. **Actions → Deploy → Run workflow**（Use workflow from: `master`）
   → https://fara-labs.com/ に公開

---

## 任意の設定

### www.fara-labs.com を fara-labs.com に転送する

1. **DNS → Records → Add record**: Type `AAAA` / Name `www` / IPv6 `100::` / Proxy status **Proxied（オレンジの雲）**
2. **Rules → Redirect Rules → Create rule → Templates** の **Redirect from WWW to root** を選んで保存

### 旧ドメイン app-fara.com から fara-labs.com に転送する

2026-10 にドメインを app-fara.com から fara-labs.com に移しました。古いリンクから来た人を新しいドメインに 301 で転送します（app-fara.com の契約を残している間だけ必要）。
**7. で fara-labs.com への公開と、6. で pokemon.fara-labs.com の表示を確認してから**行います。

1. **Worker から古いドメインを外す**: **Workers & Pages** → `fara-portfolio` → **Settings → Domains & Routes** に `app-fara.com` が残っていれば **…** → **Remove**。
   `fara-portfolio-develop` の `dev.app-fara.com` も同様に外します（外すと DNS レコードも自動で消えます）
2. `app-fara.com` のゾーン → **DNS → Records**
   - `pokemon` の CNAME（GitHub Pages 向け）が残っていれば削除
   - 次の 3 つを Type `AAAA` / IPv6 `100::` / **Proxied（オレンジの雲）** で追加: `@`・`www`・`pokemon`（同じ名前のレコードが残っていたら先に削除）
3. **Rules → Redirect Rules → Create rule**（Single Redirect）
   - **If**: Custom filter expression → `(http.host in {"app-fara.com" "www.app-fara.com"})`
     **Then**: Dynamic → `concat("https://fara-labs.com", http.request.uri.path)` / Status `301` / **Preserve query string** にチェック
   - もう 1 つ: `(http.host eq "pokemon.app-fara.com")` → `concat("https://pokemon.fara-labs.com", http.request.uri.path)` / `301` / Preserve query string
4. https://app-fara.com/ と https://pokemon.app-fara.com/ を開き、fara-labs.com 側に移ることを確認
5. （任意）3. の API トークンから `app-fara.com` のゾーンを外す

`dev.app-fara.com` は転送不要です（確認用なので 1. で外すだけで OK）。

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

### うまくいかないとき

| 症状 | 対処 |
|---|---|
| `Authentication error [code: 10000]` | トークンの貼り間違い・期限切れ、または fara-labs.com がトークンの範囲に入っていない。3. → 4. をやり直す |
| カスタムドメインの作成で失敗（already exists / conflict） | 2. の DNS レコードが残っている。削除してから再実行 |
| カスタムドメインの作成で権限エラー | ダッシュボードの **Workers & Pages → 該当 Worker → Settings → Domains & Routes → Add → Custom domain** から 1 回だけ手動で追加し、再実行 |
| Deploy が「master から実行してください」で失敗 | Run workflow のときに **Use workflow from** を `master` にする |

## ローカルで Cloudflare と同じ動きを確認する

```bash
npm ci            # 初回のみ（wrangler をインストール）
npm run preview   # dotnet publish → wrangler dev（http://localhost:8787）
npm run check     # 公開設定の検証（Cloudflare にはつながない）
```

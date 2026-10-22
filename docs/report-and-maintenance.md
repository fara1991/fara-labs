# 作者への報告とメンテナンス画面の設定（Cloudflare・Discord）

3 つのサイト（Pokémon Battle Assistant・Avatar Modding Guide・FaraBotModerator）に共通の設定です。
コードの側（フッター・`/report`・`/privacy`・`POST /api/report`）は、各リポジトリに入っています。**ここに書いた設定をするまで、報告ページは「いまは報告を受け付けていません」と表示されます**（壊れてはいません）。

---

## 1. 作者への報告（Discord に通知する）

報告フォーム → サイトの Worker（`/api/report`）→ Cloudflare Turnstile で確認 → Discord に通知、という流れです。報告の内容は保存しません（Discord にだけ残ります）。

### 1-1. Discord の Webhook を作る

1. 通知を受けたい Discord のサーバー（自分用の小さなサーバーでよい）のチャンネルを選び、「チャンネルの編集」→「連携サービス」→「ウェブフック」→「新しいウェブフック」。
2. 名前を付けて（例: サイトの報告）、「ウェブフック URL をコピー」。
3. **この URL は秘密です。** 知られると、誰でもそのチャンネルに書き込めます。漏れたときは、そのウェブフックを削除して作り直します。
4. スマホで通知が欲しいときは、そのチャンネルの通知を「すべてのメッセージ」にしておきます。

### 1-2. Cloudflare Turnstile（ボット判定）を作る

1. Cloudflare のダッシュボード →「Turnstile」→「ウィジェットを追加」。
2. 名前: `fara sites report`、ホスト名に次の 6 つを追加します。
   - `pokemon.fara-labs.com` / `pokemon-dev.fara-labs.com`
   - `avatar.fara-labs.com` / `avatar-dev.fara-labs.com`
   - `twitch-bot.fara-labs.com` / `twitch-bot-dev.fara-labs.com`
3. ウィジェットのモードは「Managed（管理）」にします。作成すると、**サイトキー**（公開してよい）と**シークレットキー**（秘密）が表示されます。

### 1-3. 6 つの Worker に値を登録する

Cloudflare のダッシュボード →「Workers & Pages」→ 次の Worker を 1 つずつ開き、「設定」→「変数とシークレット」→「追加」で、下の 3 つを登録します（**種類は 3 つとも「シークレット」**にしてください。理由は下の注意）。

| Worker | サイト |
| --- | --- |
| `fara-pokemon` | https://pokemon.fara-labs.com/ |
| `fara-pokemon-dev` | https://pokemon-dev.fara-labs.com/ |
| `fara-avatar` | https://avatar.fara-labs.com/ |
| `fara-avatar-dev` | https://avatar-dev.fara-labs.com/ |
| `fara-twitch-bot` | https://twitch-bot.fara-labs.com/ |
| `fara-twitch-bot-dev` | https://twitch-bot-dev.fara-labs.com/ |

| 名前 | 値 |
| --- | --- |
| `TURNSTILE_SITE_KEY` | Turnstile のサイトキー |
| `TURNSTILE_SECRET` | Turnstile のシークレットキー |
| `DISCORD_WEBHOOK_URL` | 1-1 でコピーした Webhook の URL |

> **注意（種類は「シークレット」）**: Wrangler で公開（`wrangler deploy`）すると、ダッシュボードで足した「テキスト」の変数は、設定ファイルに無いので消えることがあります。「シークレット」は公開しても消えません。サイトキーは公開してよい値ですが、消えないように、これもシークレットにしておきます。

### 1-4. 動作を確かめる

1. 確認用のサイト（例: https://pokemon-dev.fara-labs.com/）を開き、フッターの「作者へ報告」を押します。
2. 「ロボットではありません」の確認が出て、「いまは報告を受け付けていません」の表示が**消えている**ことを確かめます（消えない場合は、1-3 の値の登録と、Turnstile のホスト名を見直します）。
3. 種類・内容を入れて送信 → Discord にメッセージ（サイト名・種類・内容・ページ）が届けば完了です。
4. 本番のサイトでも同じように 1 回確かめます。

### 困ったとき

- **「確認に失敗しました」**: Turnstile のホスト名に、そのサイトのホスト名が入っているか。シークレットキーが正しいか。
- **「送信に失敗しました」**: Discord の Webhook URL が正しいか（削除していないか）。
- **スパムが来る**: Turnstile で大半は防げますが、確実ではありません。次の「レート制限」を入れてください。それでも続くときは、Discord の該当ウェブフックを削除して作り直します（URL が変わるので、1-3 の `DISCORD_WEBHOOK_URL` も更新）。

### 1-5. レート制限（おすすめ）

短い時間に大量に送られても、Discord が受け付けなくなって、正当な報告まで失敗しないようにします。

1. Cloudflare のダッシュボード → `fara-labs.com` →「セキュリティ」→「WAF」→「レート制限ルール」→「ルールを作成」。
2. 条件: `(http.request.uri.path eq "/api/report" and http.request.method eq "POST")`
3. 同じ IP から 10 分に 5 回まで（無料プランで選べる範囲の期間・回数で構いません）。超えたら「ブロック」。
4. ルール名は `report-rate-limit` など。無料プランのルール数には上限があります。

---

## 2. メンテナンス画面（案 A: Cloudflare の WAF ルール・コードなし）

普段は**無効**にしておき、メンテナンスしたいときにスイッチを**有効**にするだけで、サイトの代わりにメンテナンス画面（503）を返します。

### 2-1. ルールを作る（最初の 1 回だけ）

1. Cloudflare のダッシュボード → `fara-labs.com` →「セキュリティ」→「WAF」→「カスタムルール」→「ルールを作成」。
2. ルール名: `maintenance`
3. 条件（「式を編集」に貼り付け）:

   ```
   (http.host in {"pokemon.fara-labs.com" "avatar.fara-labs.com" "twitch-bot.fara-labs.com" "fara-labs.com"})
   ```

   - 一部のサイトだけにしたいときは、`{ }` の中のホスト名を減らします。
   - 確認用（`*-dev`）も止めたいときは、そのホスト名も足します。
   - **自分だけは見られるようにしたい**ときは、末尾に ` and not (ip.src eq あなたのIPアドレス)` を足します。
   - **配信用 BOT のデータ共有（`/api/my/*`）は止めたくない**ときは、末尾に ` and not starts_with(http.request.uri.path, "/api/")` を足します（足さないと、メンテナンス中は BOT からの登録データの読み書きが 503 で失敗します）。

4. アクション:「ブロック」→「カスタム応答」を選び、次のように設定します。
   - コンテンツタイプ: `text/html`
   - 応答コード: `503`
   - 本文: [`docs/maintenance.html`](maintenance.html) の中身をそのまま貼り付け（文面は、あとから自由に直せます）
5. **有効にしないまま保存します。** 保存の画面に「下書き」などの選択があればそれを選び、無ければ、作成したあと、ルール一覧で右のスイッチを**オフ**にします（画面の表記は変わることがあります）。

### 2-2. メンテナンスを始める・終える

- **始める**: 「セキュリティ」→「WAF」→「カスタムルール」で、`maintenance` の右のスイッチを**オン**。数秒〜1 分ほどで、対象のサイトがメンテナンス画面になります。
- **終える**: 同じスイッチを**オフ**。すぐに元に戻ります。
- 503 の応答は、検索エンジンに「一時的なもの」と伝わり、順位には影響しません（長期間続けない限り）。

### 2-3. 確認

オンにしたあと、ブラウザでサイトを開き、メンテナンス画面が出ることを確かめます（自分の IP を除外した場合は、スマホの回線など別の回線から）。確認したら、必要なければすぐオフに戻します。

### 補足

- 無料プランのカスタムルールは 5 つまでです。このルールで 1 つ使います。
- カスタム応答の本文には大きさの上限があります（Cloudflare の画面に表示されます）。`maintenance.html`（作者の Twitter へのボタン付き）は 1 KB 未満にしてあるので、文面を直すときも短く保ってください。デザインを凝りたいときは、案 B（各サイトの Worker で、変数 `MAINTENANCE=1` のときだけ専用ページを返す）に切り替えられます。

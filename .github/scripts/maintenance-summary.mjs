// 公開のあとの概要（GITHUB_STEP_SUMMARY）に書く、Worker の変数の反映後の現在値。2 つのワークフローで同じファイルを使う。
//
//   node .github/scripts/maintenance-summary.mjs
//   環境変数: CLOUDFLARE_API_TOKEN, CLOUDFLARE_ACCOUNT_ID, WORKER_NAME（読み取りの権限だけで足りる）
//
// 取れなかったときは「取得できませんでした」の 1 行を出す（公開の結果には影響させない）。

/** Worker の設定（bindings）から、概要の 1 行を作る（変数の値は JSON の文字列にし、バッククォートは外す）。 */
export function describeBindings(bindings) {
  const list = Array.isArray(bindings) ? bindings : [];
  const value = (name) => {
    const binding = list.find((b) => b?.name === name);
    if (!binding) return "(未設定)";
    return binding.type === "plain_text" ? JSON.stringify(binding.text).replaceAll("`", "'") : `(${binding.type})`;
  };
  return `- 反映後の現在値: MAINTENANCE_MODE=\`${value("MAINTENANCE_MODE")}\` / MAINTENANCE_MESSAGE=\`${value("MAINTENANCE_MESSAGE")}\``;
}

// コマンドラインから実行されたとき
if (process.argv[1]?.endsWith("maintenance-summary.mjs")) {
  const { CLOUDFLARE_API_TOKEN: token, CLOUDFLARE_ACCOUNT_ID: accountId, WORKER_NAME: workerName } = process.env;
  const url = `https://api.cloudflare.com/client/v4/accounts/${accountId}/workers/scripts/${workerName}/settings`;
  fetch(url, { headers: { Authorization: `Bearer ${token}` }, signal: AbortSignal.timeout(15000) })
    .then((res) => res.json())
    .then((body) => console.log(describeBindings(body?.result?.bindings)))
    .catch(() => console.log("- 反映後の現在値: 取得できませんでした"));
}

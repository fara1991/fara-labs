// 公開のあとの概要（GITHUB_STEP_SUMMARY）に書く、Worker の変数の反映後の現在値。2 つのワークフローで同じファイルを使う。
//
//   node .github/scripts/maintenance-summary.mjs
//   環境変数: CLOUDFLARE_API_TOKEN, CLOUDFLARE_ACCOUNT_ID, WORKER_NAME（読み取りの権限だけで足りる）
//
// 取れなかったときは「取得できませんでした」の 1 行を出す（公開の結果には影響させない）。
// 通信は成功しても、応答が成功（success: true）でなければ、取れなかったものとして扱う（bindings が無いのを「未設定」と誤表示しないため）。

export const UNAVAILABLE_LINE = "- 反映後の現在値: 取得できませんでした";

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

/** Cloudflare の API から Worker の設定を読み、概要の 1 行を返す。取れなかったとき（通信・応答の失敗）は UNAVAILABLE_LINE。 */
export async function fetchSummary({ token, accountId, workerName, fetchImpl = fetch }) {
  const url = `https://api.cloudflare.com/client/v4/accounts/${encodeURIComponent(accountId)}/workers/scripts/${encodeURIComponent(workerName)}/settings`;
  try {
    const res = await fetchImpl(url, { headers: { Authorization: `Bearer ${token}`, Accept: "application/json" }, signal: AbortSignal.timeout(15000) });
    if (!res.ok) return UNAVAILABLE_LINE;
    const body = await res.json();
    if (body?.success !== true) return UNAVAILABLE_LINE;
    return describeBindings(body?.result?.bindings);
  } catch {
    return UNAVAILABLE_LINE;
  }
}

// コマンドラインから実行されたとき
if (process.argv[1]?.endsWith("maintenance-summary.mjs")) {
  const { CLOUDFLARE_API_TOKEN: token, CLOUDFLARE_ACCOUNT_ID: accountId, WORKER_NAME: workerName } = process.env;
  fetchSummary({ token, accountId, workerName }).then((line) => console.log(line));
}

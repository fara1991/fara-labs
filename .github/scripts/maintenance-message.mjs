// メンテナンス表示の文（手動実行の入力 maintenance_message）を、Worker の変数（--var）に渡す前に確かめる。
// 2 つのワークフロー（deploy.yml・deploy-develop.yml）で同じファイルを使う。
//
//   node .github/scripts/maintenance-message.mjs   （環境変数 MAINTENANCE_MESSAGE_INPUT を見る）
//
// 決まり: 200 文字まで（文字数。絵文字などの 2 文字分の記号も 1 文字と数える）・改行なし。空は「変更しない」なので通す。
// 改行とみなす文字は pokemon の check-maintenance-message.mjs と同じ（CR・LF・NEL・行区切り・段落区切り）。
// 違うときは、公開の前に止める（--var の値が途中で切れたり、改行で引数が分かれたりしないように）。

export const MAX_MESSAGE_LENGTH = 200;

// 改行とみなす文字（CR・LF・NEL・行区切り・段落区切り）。コードポイントで持つ
const LINE_BREAK_CODE_POINTS = [0x0d, 0x0a, 0x85, 0x2028, 0x2029];

/** 問題があれば、その説明（日本語の 1 行）を返す。問題がなければ null。 */
export function checkMessage(text) {
  const value = String(text ?? "");
  if (value === "") return null;
  const chars = [...value];
  if (chars.some((c) => LINE_BREAK_CODE_POINTS.includes(c.codePointAt(0)))) return "改行を含められません（1 行で入力してください）";
  const length = chars.length;
  if (length > MAX_MESSAGE_LENGTH) return `${MAX_MESSAGE_LENGTH} 文字までです（いま ${length} 文字）`;
  return null;
}

// コマンドラインから実行されたとき
if (process.argv[1]?.endsWith("maintenance-message.mjs")) {
  const problem = checkMessage(process.env.MAINTENANCE_MESSAGE_INPUT);
  if (problem) {
    console.log(`::error::メンテナンス表示の文を使えません: ${problem}`);
    process.exit(1);
  }
  console.log("メンテナンス表示の文: 確かめました（空、または決まりに合っています）");
}

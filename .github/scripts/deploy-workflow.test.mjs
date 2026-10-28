// deploy.yml / deploy-develop.yml の、メンテナンス表示の渡し方（--var）とゲートの並びのテスト。
//   node --test .github/scripts/deploy-workflow.test.mjs
// 公開のステップの run を、実際に bash で動かす（npx は引数を出すだけの関数に差し替える）。
import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { checkMessage, MAX_MESSAGE_LENGTH } from "./maintenance-message.mjs";
import { describeBindings } from "./maintenance-summary.mjs";

const read = (name) => readFileSync(new URL(`../workflows/${name}`, import.meta.url), "utf8").replace(/\r\n/g, "\n");

/** "- name: <name>" から次のステップの手前までのテキスト。 */
function stepBlock(text, name) {
  const lines = text.split("\n");
  const start = lines.findIndex((l) => l.trim() === `- name: ${name}` && l.startsWith("      - "));
  assert.notEqual(start, -1, `ステップ ${name} がありません`);
  let end = lines.length;
  for (let i = start + 1; i < lines.length; i++) if (/^ {6}- /.test(lines[i])) { end = i; break; }
  return lines.slice(start, end).join("\n");
}

/** ステップの run: | の本文。 */
function runScript(block) {
  const lines = block.split("\n");
  const i = lines.findIndex((l) => l.trim() === "run: |");
  assert.notEqual(i, -1, "run: | がありません");
  const body = [];
  for (let j = i + 1; j < lines.length && (lines[j] === "" || lines[j].startsWith("          ")); j++) body.push(lines[j].slice(10));
  return body.join("\n");
}

/** run を bash で動かし、wrangler に渡る引数を返す。 */
function deployArgs(script, env) {
  const r = spawnSync("bash", ["-e", "-o", "pipefail", "-c", `npx() { printf '%s\n' "$@"; }\n${script}`], {
    env: { PATH: process.env.PATH, ...env }, encoding: "utf8",
  });
  assert.equal(r.status, 0, r.stderr);
  return r.stdout.split("\n").filter(Boolean);
}

const prod = read("deploy.yml");
const dev = read("deploy-develop.yml");
const prodScript = runScript(stepBlock(prod, "Deploy to Cloudflare Workers"));
const devScript = runScript(stepBlock(dev, "Deploy to Cloudflare Workers (develop)"));
const P = (mode = "", message = "", skip = "") => deployArgs(prodScript, { MAINTENANCE_MODE_INPUT: mode, MAINTENANCE_MESSAGE_INPUT: message, SKIP_MAINTENANCE_CHECK: skip });
const D = (mode = "", message = "") => deployArgs(devScript, { MAINTENANCE_MODE_INPUT: mode, MAINTENANCE_MESSAGE_INPUT: message });
const BASE = ["wrangler", "deploy", "--env="];

test("本番: push・何も指定しない手動実行は、公開と同時に MAINTENANCE_MODE:true", () => {
  assert.deepEqual(P(), [...BASE, "--var", "MAINTENANCE_MODE:true"]);
  assert.deepEqual(P("", "", "false"), [...BASE, "--var", "MAINTENANCE_MODE:true"]);
});

test("本番: skip_maintenance_check だけなら MAINTENANCE_MODE を渡さない", () => {
  assert.deepEqual(P("", "", "true"), BASE);
});

test("本番: mode を指定した手動実行は、その値（message があれば添える）。skip と併用でも mode は渡す", () => {
  for (const m of ["true", "notice", "false"]) assert.deepEqual(P(m), [...BASE, "--var", `MAINTENANCE_MODE:${m}`]);
  assert.deepEqual(P("notice", "10月25日に更新します"), [...BASE, "--var", "MAINTENANCE_MODE:notice", "--var", "MAINTENANCE_MESSAGE:10月25日に更新します"]);
  assert.deepEqual(P("false", "", "true"), [...BASE, "--var", "MAINTENANCE_MODE:false"]);
});

test("本番: message だけの手動実行は MAINTENANCE_MODE を渡さない（メンテナンス画面にしない）", () => {
  assert.deepEqual(P("", "お知らせ"), [...BASE, "--var", "MAINTENANCE_MESSAGE:お知らせ"]);
  assert.deepEqual(P("", "お知らせ", "true"), [...BASE, "--var", "MAINTENANCE_MESSAGE:お知らせ"]);
});

test("本番: 文面の記号・空白・: はそのまま 1 つの引数として渡る（シェルに解釈されない）", () => {
  const msg = "20:00 ごろ $(echo x) `y` \"z\" ; & *";
  assert.deepEqual(P("notice", msg), [...BASE, "--var", "MAINTENANCE_MODE:notice", "--var", `MAINTENANCE_MESSAGE:${msg}`]);
  assert.deepEqual(D("", msg), ["wrangler", "deploy", "--env", "develop", "--var", `MAINTENANCE_MESSAGE:${msg}`]);
});

test("dev: 入力が空なら --var を付けない。あれば付ける", () => {
  assert.deepEqual(D(), ["wrangler", "deploy", "--env", "develop"]);
  assert.deepEqual(D("notice", "更新予定"), ["wrangler", "deploy", "--env", "develop", "--var", "MAINTENANCE_MODE:notice", "--var", "MAINTENANCE_MESSAGE:更新予定"]);
});

test("本番: ゲートは、mode が true / notice / false のときだけ飛ばす。ステップの順序（Require master → checkout → ゲート → 公開 → サマリー）", () => {
  const gate = stepBlock(prod, "Require MAINTENANCE_MODE=notice");
  assert.match(gate, /if: \$\{\{ !contains\(fromJSON\('\["true","notice","false"\]'\), inputs\.maintenance_mode\) \}\}/);
  assert.match(gate, /SKIP_MAINTENANCE_CHECK: \$\{\{ inputs\.skip_maintenance_check \}\}/);
  assert.match(gate, /require-maintenance\.mjs fara-portfolio/);
  // 初回の公開（Worker がまだ無い）だけ、first_publish で 404 を通す。それ以外は 404 で止まる
  assert.match(gate, /ALLOW_MISSING_WORKER: \$\{\{ inputs\.first_publish && '1' \|\| '0' \}\}/);
  assert.match(prod, /first_publish:\n {8}description:[^\n]*\n {8}type: boolean\n {8}default: false/);
  const order = ["Require master", "Test (data files)", "Publish static site", "Require MAINTENANCE_MODE=notice", "Check maintenance message", "Deploy to Cloudflare Workers", "Summary (maintenance)"]
    .map((n) => prod.indexOf(`- name: ${n}\n`));
  assert.ok(order.every((v) => v >= 0), "ステップが足りません");
  assert.deepEqual([...order].sort((a, b) => a - b), order, "ステップの順序が違います");
  assert.match(stepBlock(prod, "Require master"), /if: github\.ref != 'refs\/heads\/master'/);
});

test("ワークフロー: 旧名 MAINTENANCE は使わない。入力・サマリーがある。サマリーは、文の検査が通ったときだけ（always() かつ検査の成功）", () => {
  for (const text of [prod, dev]) {
    assert.doesNotMatch(text, /(?<![A-Z_])MAINTENANCE(?![A-Z_])/);
    assert.match(text, /maintenance_mode:\n {8}description:[^\n]*\n {8}type: choice\n {8}options: \["", "true", "notice", "false"\]\n {8}default: ""/);
    assert.match(text, /maintenance_message:\n {8}description:[^\n]*\n {8}type: string\n {8}default: ""/);
    // 文の検査が失敗したら、未検査の文を概要に書かない（公開だけが失敗した場合は書く）
    assert.match(stepBlock(text, "Summary (maintenance)"), /if: \$\{\{ always\(\) && steps\.message\.outcome == 'success' \}\}/);
    assert.match(stepBlock(text, "Check maintenance message"), /\n {8}id: message\n/);
  }
  assert.match(stepBlock(prod, "Summary (maintenance)"), /WORKER_NAME: fara-portfolio\n/);
  assert.match(stepBlock(dev, "Summary (maintenance)"), /WORKER_NAME: fara-portfolio-dev\n/);
  assert.match(prod, /skip_maintenance_check:\n[\s\S]*?type: boolean/);
});

test("wrangler.jsonc: run_worker_first と keep_vars を維持する", () => {
  const w = readFileSync(new URL("../../wrangler.jsonc", import.meta.url), "utf8");
  assert.match(w, /"keep_vars": true/);
  assert.match(w, /"run_worker_first": true/);
});

test("メンテナンス表示の文: 空は通す。200 文字まで（文字数で数える）・改行や制御文字は止める", () => {
  assert.equal(checkMessage(""), null);
  assert.equal(checkMessage(undefined), null);
  assert.equal(checkMessage("a".repeat(MAX_MESSAGE_LENGTH)), null);
  assert.match(checkMessage("a".repeat(MAX_MESSAGE_LENGTH + 1)), /200 文字までです（いま 201 文字）/);
  // 絵文字（サロゲートの組）は 1 文字と数える
  assert.equal(checkMessage("😀".repeat(MAX_MESSAGE_LENGTH)), null);
  assert.match(checkMessage("1行目\n2行目"), /改行/);
});

test("メンテナンス表示の文: 改行は pokemon と同じ集合（CR・LF・NEL・U+2028・U+2029）を止める。タブなどの他の文字は通す", () => {
  for (const cp of [0x0d, 0x0a, 0x85, 0x2028, 0x2029]) {
    assert.match(checkMessage(`a${String.fromCodePoint(cp)}b`), /改行/, cp.toString(16));
  }
  assert.equal(checkMessage("タブ\tあり"), null);
});

test("サマリー（メンテナンス表示）: 文面の Markdown の特殊文字（バッククォート・< >・改行）を落として書く", () => {
  const summary = runScript(stepBlock(prod, "Summary (maintenance)"));
  const dir = mkdtempSync(join(tmpdir(), "maint-summary-"));
  try {
    const file = join(dir, "summary.md");
    // node（概要の取得）は関数に差し替える。書き先は GITHUB_STEP_SUMMARY（一時ファイル）
    const r = spawnSync("bash", ["-e", "-o", "pipefail", "-c", `node() { echo "- 反映後の現在値: 確認"; }\n${summary}`], {
      env: { PATH: process.env.PATH, GITHUB_STEP_SUMMARY: file, WORKER_NAME: "fara-portfolio", MAINTENANCE_MESSAGE_INPUT: "a`b<c>d\ne", MAINTENANCE_MODE_INPUT: "notice" },
      encoding: "utf8",
    });
    assert.equal(r.status, 0, r.stderr);
    const out = readFileSync(file, "utf8");
    assert.match(out, /MAINTENANCE_MESSAGE=`a"bcde`/);
    assert.doesNotMatch(out, /[<>]/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("メンテナンス表示の文: スクリプトを実行したとき、問題があれば終了コード 1 と ::error::", () => {
  const run = (message) => spawnSync(process.execPath, [fileURLToPath(new URL("./maintenance-message.mjs", import.meta.url))], {
    env: { PATH: process.env.PATH, MAINTENANCE_MESSAGE_INPUT: message }, encoding: "utf8",
  });
  assert.equal(run("お知らせ").status, 0);
  const bad = run("a\nb");
  assert.equal(bad.status, 1);
  assert.match(bad.stdout, /::error::/);
});

test("反映後の現在値: 変数の値を JSON の文字列で出し、バッククォートは外す。無いものは（未設定）", () => {
  const line = describeBindings([
    { name: "MAINTENANCE_MODE", type: "plain_text", text: "notice" },
    { name: "MAINTENANCE_MESSAGE", type: "plain_text", text: "は`x`" },
  ]);
  assert.equal(line, "- 反映後の現在値: MAINTENANCE_MODE=`\"notice\"` / MAINTENANCE_MESSAGE=`\"は'x'\"`");
  assert.equal(describeBindings([{ name: "MAINTENANCE_MODE", type: "secret_text" }]), "- 反映後の現在値: MAINTENANCE_MODE=`(secret_text)` / MAINTENANCE_MESSAGE=`(未設定)`");
  assert.equal(describeBindings(undefined), "- 反映後の現在値: MAINTENANCE_MODE=`(未設定)` / MAINTENANCE_MESSAGE=`(未設定)`");
  // plain_text なのに text が無いときも例外にせず「不明」
  assert.equal(describeBindings([{ name: "MAINTENANCE_MODE", type: "plain_text" }]), "- 反映後の現在値: MAINTENANCE_MODE=`(不明)` / MAINTENANCE_MESSAGE=`(未設定)`");
});

test("ワークフロー: 概要は共通のスクリプトを使い（node -e の重複なし）、develop は取り消さない（本番と同じ）", () => {
  for (const text of [prod, dev]) {
    assert.match(text, /node \.github\/scripts\/maintenance-summary\.mjs/);
    assert.doesNotMatch(text, /node -e/);
    assert.match(text, /node \.github\/scripts\/maintenance-message\.mjs/);
  }
  assert.match(dev, /cancel-in-progress: false/);
  assert.match(prod, /cancel-in-progress: false/);
});

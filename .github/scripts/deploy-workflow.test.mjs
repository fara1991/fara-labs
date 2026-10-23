// deploy.yml / deploy-develop.yml の、メンテナンス表示の渡し方（--var）とゲートの並びのテスト。
//   node --test .github/scripts/deploy-workflow.test.mjs
// 公開のステップの run を、実際に bash で動かす（npx は引数を出すだけの関数に差し替える）。
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { spawnSync } from "node:child_process";

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
  const order = ["Require master", "Test (data files)", "Publish static site", "Require MAINTENANCE_MODE=notice", "Deploy to Cloudflare Workers", "Summary (maintenance)"]
    .map((n) => prod.indexOf(`- name: ${n}\n`));
  assert.ok(order.every((v) => v >= 0), "ステップが足りません");
  assert.deepEqual([...order].sort((a, b) => a - b), order, "ステップの順序が違います");
  assert.match(stepBlock(prod, "Require master"), /if: github\.ref != 'refs\/heads\/master'/);
});

test("ワークフロー: 旧名 MAINTENANCE は使わない。入力・サマリーがある。サマリーは always()", () => {
  for (const text of [prod, dev]) {
    assert.doesNotMatch(text, /(?<![A-Z_])MAINTENANCE(?![A-Z_])/);
    assert.match(text, /maintenance_mode:\n {8}description:[^\n]*\n {8}type: choice\n {8}options: \["", "true", "notice", "false"\]\n {8}default: ""/);
    assert.match(text, /maintenance_message:\n {8}description:[^\n]*\n {8}type: string\n {8}default: ""/);
    assert.match(stepBlock(text, "Summary (maintenance)"), /if: \$\{\{ always\(\) \}\}/);
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

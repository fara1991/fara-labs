// 「問い合わせ」ページの送信部分（wwwroot/js/report.js）の、純粋な関数のテスト。  node --test tests/js
// 画面の送信（Turnstile・fetch）は、ブラウザで確かめる（docs/report-and-maintenance.md の確認項目）。
const { test } = require("node:test");
const assert = require("node:assert/strict");
const report = require("../../src/FaraPortfolio.Web/wwwroot/js/report.js");

const LIMITS = { min: 5, max: 2000, maxContact: 200 };
const IDS = ["bug", "request", "other"];

test("文字数は、絵文字を 1 文字として数える（UTF-16 の 2 単位ではない）", () => {
  assert.equal(report.codePoints("abc"), 3);
  assert.equal(report.codePoints("😀😀😀😀😀"), 5);
});

test("入力の確認: 種類が無いとき、種類を選ぶように言う", () => {
  assert.equal(report.problemOf({ category: "", message: "十分な長さの本文", contact: "" }, LIMITS, IDS), "種類を選んでください");
  assert.equal(report.problemOf({ category: "nope", message: "十分な長さの本文", contact: "" }, LIMITS, IDS), "種類を選んでください");
});

test("入力の確認: 本文が短すぎる・長すぎるとき、文字数を言う（前後の空白は数えない）", () => {
  assert.equal(report.problemOf({ category: "bug", message: "  abcd  ", contact: "" }, LIMITS, IDS), "内容を 5 文字以上で書いてください");
  assert.equal(report.problemOf({ category: "bug", message: "abcde", contact: "" }, LIMITS, IDS), "");
  const long = "あ".repeat(2001);
  assert.equal(report.problemOf({ category: "bug", message: long, contact: "" }, LIMITS, IDS), "内容は 2000 文字までにしてください（いま 2001 文字）");
  assert.equal(report.problemOf({ category: "bug", message: "あ".repeat(2000), contact: "" }, LIMITS, IDS), "");
});

test("入力の確認: 絵文字は、サーバーと同じく文字単位で数える", () => {
  const emoji = "😀".repeat(2000);
  assert.equal(report.problemOf({ category: "bug", message: emoji, contact: "" }, LIMITS, IDS), "");
});

test("入力の確認: 連絡先が長すぎるとき言う", () => {
  assert.equal(report.problemOf({ category: "request", message: "十分な長さの本文", contact: "x".repeat(201) }, LIMITS, IDS), "連絡先は 200 文字までにしてください");
});

test("ページのパスは、同じサイトのパス（英数字・- _ / .）だけ送る", () => {
  assert.equal(report.cleanPage("/works"), "/works");
  assert.equal(report.cleanPage("/a/b_c-d.e"), "/a/b_c-d.e");
  assert.equal(report.cleanPage(null), "");
  assert.equal(report.cleanPage("//evil.example"), "");
  assert.equal(report.cleanPage("https://evil.example/"), "");
  assert.equal(report.cleanPage("/<script>"), "");
  assert.equal(report.cleanPage("/" + "a".repeat(300)), "");
});

test("サーバーの応答から、画面に出す文を取り出す（読めなければ状態に合わせた既定の文）", () => {
  assert.equal(report.describeFailure(400, '{"message":"本文が短すぎます"}'), "本文が短すぎます");
  assert.equal(report.describeFailure(429, "not json"), "短い間に何度も送信されました。少し待ってから、もう一度お試しください");
  assert.equal(report.describeFailure(413, ""), "内容が長すぎます。短くして、もう一度お試しください");
  assert.equal(report.describeFailure(503, "{}"), "いまは報告を受け付けていません。しばらくしてからお試しください");
  assert.equal(report.describeFailure(502, ""), "送信に失敗しました。しばらくしてからもう一度お試しください");
  assert.equal(report.describeFailure(403, ""), "送信できませんでした。入力を確かめて、もう一度お試しください");
});

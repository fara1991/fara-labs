// maintenance-summary.mjs のテスト（公開のあとの概要）。  node --test .github/scripts/maintenance-summary.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { fetchSummary, UNAVAILABLE_LINE } from "./maintenance-summary.mjs";

const ENV = { token: "tok", accountId: "acc", workerName: "fara-x" };
const ok = (bindings) => new Response(JSON.stringify({ success: true, result: { bindings } }), { status: 200 });

test("fetchSummary: 成功のとき、変数の現在値の 1 行を返す。URL は ID を符号化し、トークンで認証する", async () => {
  let seen;
  const line = await fetchSummary({
    ...ENV,
    fetchImpl: async (url, init) => {
      seen = { url, init };
      return ok([{ name: "MAINTENANCE_MODE", type: "plain_text", text: "notice" }]);
    },
  });
  assert.equal(seen.url, "https://api.cloudflare.com/client/v4/accounts/acc/workers/scripts/fara-x/settings");
  assert.equal(seen.init.headers.Authorization, "Bearer tok");
  assert.match(line, /MAINTENANCE_MODE=`"notice"`/);
  assert.match(line, /MAINTENANCE_MESSAGE=`\(未設定\)`/);
});

test("fetchSummary: 応答が失敗（403 など）のときは「取得できませんでした」。「未設定」と誤表示しない", async () => {
  const line = await fetchSummary({ ...ENV, fetchImpl: async () => new Response(JSON.stringify({ success: false, result: {} }), { status: 403 }) });
  assert.equal(line, UNAVAILABLE_LINE);
  assert.ok(!line.includes("未設定"));
});

test("fetchSummary: 200 でも success:false（bindings が無い）のときは「取得できませんでした」", async () => {
  const line = await fetchSummary({ ...ENV, fetchImpl: async () => new Response(JSON.stringify({ success: false, errors: [{ code: 10000 }] }), { status: 200 }) });
  assert.equal(line, UNAVAILABLE_LINE);
});

test("fetchSummary: 通信の例外・壊れた JSON のときも「取得できませんでした」（例外を外に出さない）", async () => {
  assert.equal(await fetchSummary({ ...ENV, fetchImpl: async () => { throw new Error("network"); } }), UNAVAILABLE_LINE);
  assert.equal(await fetchSummary({ ...ENV, fetchImpl: async () => new Response("<html>", { status: 200 }) }), UNAVAILABLE_LINE);
});

test("fetchSummary: 応答にトークンを含めない", async () => {
  const line = await fetchSummary({ ...ENV, token: "super-secret-token", fetchImpl: async () => new Response("", { status: 500 }) });
  assert.ok(!line.includes("super-secret-token"));
});

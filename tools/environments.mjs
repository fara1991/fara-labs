// 公開先（本番・確認用）の共通の定数と検査。tools/build-pages.mjs と tools/prerender-pages.mjs で同じものを使う。
export const ENVIRONMENTS = ["production", "develop"];

/** --env の値を確かめる。不正なら例外（メッセージは使い方を案内する）。 */
export function assertEnvironment(env) {
  if (!ENVIRONMENTS.includes(env)) throw new Error(`--env は ${ENVIRONMENTS.join(" か ")} を指定してください`);
  return env;
}

/** 公開先に使う tools/site.json のキー（確認用は devOrigin、本番は origin）。 */
export function originKey(env) {
  return env === "develop" ? "devOrigin" : "origin";
}

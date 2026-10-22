// 「問い合わせ」ページ（Pages/Report.razor）の送信。フレームワークは使わない。
// 送信の流れ: 入力の確認 → Cloudflare Turnstile（ボット判定）の確認 → POST /api/report（worker/report.js）。
// 外部のスクリプトは https://challenges.cloudflare.com だけ（CSP は今のところ無し。_headers は変えない）。
// サイトキーは公開してよい値で、GET /api/report/config から読む。
//
// 制限（最小・最大の文字数など）は Pages/Report.razor の form の data-* 属性（Services/ReportLimits.cs の値）から読む。
// 数え方: 文字数は、サーバー（worker/report.js）と同じく文字（コードポイント）で数える。
(function () {
    'use strict';

    var TURNSTILE_SRC = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
    var TIMEOUT_MS = 15000;
    var TOKEN_WAIT_MS = 5000;

    /** 文字数（絵文字など、UTF-16 で 2 単位の文字も 1 文字として数える）。 */
    function codePoints(text) {
        return Array.from(text).length;
    }

    /** ?from= で渡されたページ（パス）を、送ってよい形にする。パスでなければ空。worker/report.js の cleanPage と同じ条件。 */
    function cleanPage(from) {
        var path = String(from || '').trim();
        if (path.length === 0 || path.length > 300) return '';
        if (path.charAt(0) !== '/' || path.indexOf('//') === 0) return '';
        return /^[A-Za-z0-9\-_/.]*$/.test(path) ? path : '';
    }

    /**
     * 入力の問題を、人が読む文で返す。問題がなければ空文字。
     * @param {{category: string, message: string, contact: string}} values
     * @param {{min: number, max: number, maxContact: number}} limits
     * @param {string[]} categoryIds 選べる種類の ID
     */
    function problemOf(values, limits, categoryIds) {
        if (categoryIds.indexOf(values.category) < 0) return '種類を選んでください';
        var message = String(values.message || '').trim();
        var length = codePoints(message);
        if (length < limits.min) return '内容を ' + limits.min + ' 文字以上で書いてください';
        if (length > limits.max) return '内容は ' + limits.max + ' 文字までにしてください（いま ' + length + ' 文字）';
        if (codePoints(String(values.contact || '').trim()) > limits.maxContact) return '連絡先は ' + limits.maxContact + ' 文字までにしてください';
        return '';
    }

    /** サーバーの応答から、画面に出す文を取り出す（{"message": "..."}。読めなければ状態コードに合わせた既定の文）。 */
    function describeFailure(status, body) {
        try {
            var parsed = JSON.parse(body);
            if (parsed && typeof parsed.message === 'string' && parsed.message.trim()) return parsed.message;
        } catch (e) {
            // 読めない応答は、下の既定の文にする
        }
        if (status === 413) return '内容が長すぎます。短くして、もう一度お試しください';
        if (status === 429) return '短い間に何度も送信されました。少し待ってから、もう一度お試しください';
        if (status === 503) return 'いまは報告を受け付けていません。しばらくしてからお試しください';
        if (status >= 500) return '送信に失敗しました。しばらくしてからもう一度お試しください';
        return '送信できませんでした。入力を確かめて、もう一度お試しください';
    }

    // テスト（node --test tests/js）から、純粋な関数だけを読めるようにする。ブラウザでは module が無いので何もしない
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = { codePoints: codePoints, cleanPage: cleanPage, problemOf: problemOf, describeFailure: describeFailure };
        return;
    }

    var loading = null;
    function loadTurnstile() {
        if (window.turnstile) return Promise.resolve();
        if (loading) return loading;
        loading = new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = TURNSTILE_SRC;
            s.async = true;
            s.onload = function () { resolve(); };
            s.onerror = function () { loading = null; reject(new Error('turnstile')); };
            document.head.appendChild(s);
        });
        return loading;
    }

    function init() {
        var form = document.getElementById('report-form');
        if (!form) return;

        var submit = document.getElementById('report-submit');
        var messageField = document.getElementById('report-message');
        var count = document.getElementById('report-count');
        var errorEl = document.getElementById('report-error');
        var disabledEl = document.getElementById('report-disabled');
        var doneEl = document.getElementById('report-done');
        var bodyEl = document.getElementById('report-body');
        var againButton = document.getElementById('report-again');
        var turnstileEl = document.getElementById('report-turnstile');

        var limits = {
            min: Number(form.dataset.min),
            max: Number(form.dataset.max),
            maxContact: Number(form.dataset.maxContact),
        };
        var categoryIds = Array.prototype.map.call(form.querySelector('select[name="category"]').options, function (o) { return o.value; })
            .filter(function (v) { return v !== ''; });
        var from = new URLSearchParams(location.search).get('from');

        var widgetId = null;
        var token = '';
        var sending = false;
        var ready = false;

        function showError(text) {
            errorEl.textContent = text;
            errorEl.hidden = !text;
        }

        function resetWidget() {
            token = '';
            if (window.turnstile && widgetId !== null) {
                try { window.turnstile.reset(widgetId); } catch (e) { /* すでに無いとき */ }
            }
        }

        function disposeWidget() {
            token = '';
            if (window.turnstile && widgetId !== null) {
                try { window.turnstile.remove(widgetId); } catch (e) { /* すでに無いとき */ }
            }
            widgetId = null;
        }

        // 確認の欄を出す。受け付けていない（設定が無い・読めない）ときは、案内を出して送信は止める
        function mount() {
            ready = false;
            submit.disabled = true;
            return fetch('/api/report/config', { headers: { Accept: 'application/json' }, cache: 'no-store' })
                .then(function (res) { return res.ok ? res.json() : null; })
                .then(function (cfg) {
                    if (!cfg || !cfg.enabled || !cfg.siteKey) throw new Error('disabled');
                    return loadTurnstile().then(function () { return cfg; });
                })
                .then(function (cfg) {
                    token = '';
                    widgetId = window.turnstile.render(turnstileEl, {
                        sitekey: cfg.siteKey,
                        callback: function (t) { token = t; },
                        'expired-callback': function () { token = ''; },
                        'error-callback': function () { token = ''; },
                    });
                    ready = true;
                    disabledEl.hidden = true;
                    submit.disabled = false;
                })
                .catch(function () {
                    disabledEl.hidden = false;
                    submit.disabled = true;
                });
        }

        // 確認のトークンが届くまで、最大 TOKEN_WAIT_MS 待つ（読み込み直後や、送信に失敗して作り直した直後は数秒かかることがある）
        function waitToken() {
            return new Promise(function (resolve) {
                var waited = 0;
                (function check() {
                    if (token) return resolve(token);
                    if (waited >= TOKEN_WAIT_MS) return resolve('');
                    waited += 200;
                    setTimeout(check, 200);
                })();
            });
        }

        messageField.addEventListener('input', function () {
            count.textContent = String(codePoints(messageField.value.trim()));
        });

        form.addEventListener('submit', function (event) {
            event.preventDefault();
            if (sending || !ready) return;
            showError('');
            var values = {
                category: form.elements.category.value,
                message: messageField.value,
                contact: form.elements.contact.value,
            };
            var problem = problemOf(values, limits, categoryIds);
            if (problem) {
                showError(problem);
                return;
            }
            sending = true;
            submit.disabled = true;
            submit.textContent = '送信中…';

            waitToken().then(function (turnstileToken) {
                if (!turnstileToken) {
                    showError('「ロボットではありません」の確認が終わっていません。表示された確認をしてから、もう一度押してください');
                    return null;
                }
                var controller = typeof AbortController === 'function' ? new AbortController() : null;
                var timer = setTimeout(function () { if (controller) controller.abort(); }, TIMEOUT_MS);
                return fetch('/api/report', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
                    body: JSON.stringify({
                        category: values.category,
                        message: values.message.trim(),
                        contact: values.contact.trim(),
                        page: cleanPage(from),
                        turnstileToken: turnstileToken,
                        website: form.elements.website.value,
                    }),
                    signal: controller ? controller.signal : undefined,
                })
                    .then(function (res) {
                        return res.text().then(function (text) { return { ok: res.ok, status: res.status, text: text }; });
                    })
                    .finally(function () { clearTimeout(timer); });
            })
                .then(function (result) {
                    if (!result) { resetWidget(); return; }
                    if (result.ok) {
                        // 送れた。フォームを隠して、お礼を出す（確認の欄は片付ける）
                        disposeWidget();
                        form.reset();
                        count.textContent = '0';
                        bodyEl.hidden = true;
                        doneEl.hidden = false;
                        doneEl.setAttribute('tabindex', '-1');
                        doneEl.focus();
                        return;
                    }
                    showError(describeFailure(result.status, result.text));
                    resetWidget();   // 確認は 1 回で使い切りなので、やり直せるように作り直す
                })
                .catch(function () {
                    // 通信の失敗・タイムアウトは、通信の失敗として扱う
                    showError('通信できませんでした。通信の状態を確かめて、もう一度お試しください');
                    resetWidget();
                })
                .finally(function () {
                    sending = false;
                    submit.textContent = '送信する';
                    submit.disabled = !ready;
                });
        });

        if (againButton) {
            againButton.addEventListener('click', function () {
                doneEl.hidden = true;
                bodyEl.hidden = false;
                showError('');
                mount();
            });
        }

        mount();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();

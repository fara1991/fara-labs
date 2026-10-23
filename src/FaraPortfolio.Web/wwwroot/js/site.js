// サイト全体の演出（Blazor WebAssembly のアプリの外側。Razor が描いた DOM に対して動く）
//  - スクロール量を CSS 変数 --progress に流す（上部の進捗バー）。スクロールするのは .site-scroll（ヘッダーの下の領域）
//  - .reveal 要素が画面に入ったら .is-visible を付ける（フェードイン）。js-ready を付けたあとだけ隠すので、失敗しても本文は見える
//  - 軌道アイコン: タッチしている間は止める。説明（.satellite-tip）が画面からはみ出すときは内側に寄せる
//  - スマホのメニュー: 開閉は Razor（Components/SiteHeader.razor）。外側タップ・Esc で閉じるのは、ここから開閉ボタンを押す
//  - works#id で来たら該当の作品の枠を 1 回光らせる
//  - 戻る・進むで、離れたときのスクロール位置に戻す（sessionStorage に一時保存する）
//
// ページ移動（Blazor のルーティング）: Layout/MainLayout.razor が、描画のあと（最初と、ページを移るたび）に
// window.faraSite.pageChanged() を呼ぶ。新しい本文の .reveal を監視し、#付き・戻る・進む・通常の位置を決める。
// 更新履歴の絞り込みと NEW は Razor（Pages/Updates.razor・Components/UpdateItem.razor）で行う。
(() => {
    'use strict';
    const root = document.documentElement;
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    const scrollEl = () => document.querySelector('.site-scroll');
    const pageKey = () => location.pathname;

    // ---- スクロール位置の保存（メモリと sessionStorage。sessionStorage が使えないときはメモリだけ）
    const STORE = 'faraScroll';
    let positions = {};
    try {
        const parsed = JSON.parse(sessionStorage.getItem(STORE) || '{}');
        positions = parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : {};
    } catch (e) {
        positions = {};
    }
    // 書き込みは間引く（ページの非表示・離脱のときにまとめて書く）
    let dirty = false;
    let flushTimer = 0;
    const flush = () => {
        clearTimeout(flushTimer);
        flushTimer = 0;
        if (!dirty) return;
        dirty = false;
        try { sessionStorage.setItem(STORE, JSON.stringify(positions)); } catch (e) { /* 保存できないときは、メモリだけ */ }
    };
    // いま位置を保存してよいページ（ページを移った直後、本文を差し替える途中のスクロールを、新しいページの位置として保存しない）
    let activeKey = pageKey();
    const savePosition = (el) => {
        if (activeKey !== pageKey()) return;
        positions[pageKey()] = el.scrollTop;
        dirty = true;
        if (!flushTimer) flushTimer = setTimeout(flush, 800);
    };
    window.addEventListener('pagehide', flush);
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'hidden') flush(); });

    // ---- 進捗バー・ヘッダーの影
    let ticking = false;
    const update = () => {
        const el = scrollEl();
        const y = el ? el.scrollTop : window.scrollY;
        const max = el ? el.scrollHeight - el.clientHeight : root.scrollHeight - window.innerHeight;
        root.style.setProperty('--progress', max > 0 ? (y / max).toFixed(4) : '0');
        root.classList.toggle('is-scrolled', y > 8);
        if (el) savePosition(el);
        ticking = false;
    };
    const onScroll = () => {
        if (!ticking) {
            ticking = true;
            requestAnimationFrame(update);
        }
    };
    // scroll は泡立たないので、捕まえ（capture）で、どの要素のスクロールも受け取る
    document.addEventListener('scroll', onScroll, { passive: true, capture: true });
    window.addEventListener('resize', onScroll, { passive: true });

    // ---- フェードイン（.reveal）。js-ready を付けるのは、監視の準備ができてから（失敗したときに本文を隠さないため）
    const observer = new IntersectionObserver((entries) => {
        for (const entry of entries) {
            if (entry.isIntersecting) {
                entry.target.classList.add('is-visible');
                observer.unobserve(entry.target);
            }
        }
    }, { root: null, threshold: 0.1, rootMargin: '0px 0px -40px 0px' });
    const initReveal = () => {
        document.querySelectorAll('.reveal:not(.is-visible)').forEach((el) => observer.observe(el));
    };
    initReveal();
    root.classList.add('js-ready');

    // ---- 作品への移動（works#id）
    const scrollToHash = () => {
        const id = decodeURIComponent(location.hash.slice(1));
        if (!id) return;
        const el = document.getElementById(id);
        if (!el) return;
        el.scrollIntoView({ behavior: reduceMotion.matches ? 'auto' : 'smooth', block: 'start' });
        // どの作品に飛んだか分かるよう、枠を 1 回だけ光らせる
        el.classList.remove('is-target');
        void el.offsetWidth;
        el.classList.add('is-target');
        el.addEventListener('animationend', () => el.classList.remove('is-target'), { once: true });
    };

    // ---- 軌道アイコン（タッチ中は止める）
    const initOrbit = () => {
        let touchTimer = 0;
        document.addEventListener('pointerdown', (e) => {
            if (e.pointerType !== 'touch' || !(e.target instanceof Element)) return;
            const visual = e.target.closest('.hero-visual');
            if (!visual) return;
            visual.classList.add('is-touching');
            clearTimeout(touchTimer);
            touchTimer = setTimeout(() => visual.classList.remove('is-touching'), 4000);
        }, { passive: true });
    };

    // ---- 軌道アイコンの説明が画面の左右からはみ出すときは内側に寄せる（説明の位置はアイコンの中心と説明の幅から計算する）
    const initTooltips = () => {
        const placeTip = (e) => {
            const sat = e.target instanceof Element ? e.target.closest('.satellite') : null;
            const tip = sat?.querySelector('.satellite-tip');
            if (!tip) return;
            const rect = sat.getBoundingClientRect();
            const center = (rect.left + rect.right) / 2;
            const half = tip.getBoundingClientRect().width / 2;
            const margin = 12;
            const overRight = center + half > root.clientWidth - margin;
            sat.classList.toggle('tip-left', overRight);
            sat.classList.toggle('tip-right', !overRight && center - half < margin);
        };
        document.addEventListener('pointerover', placeTip, { passive: true });
        document.addEventListener('focusin', placeTip);
    };

    // ---- スマホのメニュー: 外側のタップ・Esc で閉じる（開閉の状態は Razor が持つので、開閉ボタンを押す）
    const initMenu = () => {
        const toggle = () => document.querySelector('.menu-toggle');
        const menuOpen = () => toggle()?.getAttribute('aria-expanded') === 'true';
        document.addEventListener('click', (e) => {
            if (!menuOpen() || !(e.target instanceof Element)) return;
            if (e.target.closest('.site-nav, .menu-toggle')) return;
            toggle().click();
        });
        document.addEventListener('keydown', (e) => {
            if (e.key !== 'Escape' || !menuOpen()) return;
            const button = toggle();
            button.click();
            button.focus();
        });
    };

    // ---- ページの表示（読み込みのあと、または拡張ナビゲーションで差し替えたあとに呼ぶ）
    //  #付きのときは、その場所へ。戻る・進むのときは、離れたときの位置へ。通常の移動は先頭へ
    // 戻る・進む（popstate）の移動か。Blazor の通知（LocationChanged）では、リンクのクリックと区別できないので、ここで見る（Blazor より先に登録するので、先に呼ばれる）
    let popped = false;
    window.addEventListener('popstate', () => { popped = true; });
    let firstCall = true;
    const pageChanged = (backNavigation = false) => {
        backNavigation = backNavigation || popped;
        popped = false;
        if (firstCall) {
            // 最初の表示が、戻る・進むで来たとき（ほかのサイトからの戻りなど）も、離れたときの位置へ
            firstCall = false;
            const navEntry = performance.getEntriesByType ? performance.getEntriesByType('navigation')[0] : null;
            backNavigation = backNavigation || Boolean(navEntry && navEntry.type === 'back_forward');
        }
        flush();
        initReveal();
        const el = scrollEl();
        const target = backNavigation ? (positions[pageKey()] || 0) : 0;
        activeKey = pageKey();
        if (!el) return;
        if (location.hash) {
            scrollToHash();
            return;
        }
        el.scrollTo({ top: target, behavior: 'instant' });
        update();
    };
    // 「今日」（ブラウザの日付。更新履歴の NEW。Program.cs が起動のときに読む）と、スキップリンクの先
    const today = () => {
        const now = new Date();
        const two = (n) => String(n).padStart(2, '0');
        return `${now.getFullYear()}-${two(now.getMonth() + 1)}-${two(now.getDate())}`;
    };
    const focusMain = () => document.getElementById('main-content')?.focus();
    window.faraSite = { pageChanged, scrollToHash, today, focusMain };

    // 初回の読み込み。機能ごとに、失敗しても他の機能は動くようにする
    const safely = (fn) => { try { fn(); } catch (e) { /* その機能だけ止まる。本文は読める */ } };
    safely(initOrbit);
    safely(initTooltips);
    safely(initMenu);
    safely(update);
})();

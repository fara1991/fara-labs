// サイト全体の演出（書き出した HTML に付く。フレームワークは使わない。JS が無い・途中で失敗しても本文は読める）
//  - スクロール量を CSS 変数 --progress に流す（上部の進捗バー）。スクロールするのは .site-scroll（ヘッダーの下の領域）
//  - .reveal 要素が画面に入ったら .is-visible を付ける（フェードイン）。js-ready を付けたあとだけ隠すので、失敗しても本文は見える
//  - 軌道アイコン: タッチしている間は止める。説明（.satellite-tip）が画面からはみ出すときは内側に寄せる
//  - スマホのメニュー: 開閉（aria-expanded）、最初のリンクへフォーカス、外側タップ・Esc で閉じる
//  - works#id で来たら該当の作品の枠を 1 回光らせる
//  - 戻る・進むで、離れたときのスクロール位置に戻す（sessionStorage に一時保存する）
//  - 更新履歴の絞り込みと、NEW の表示（今日の日付はブラウザで数える）
//
// ページ移動: いまは通常の読み込み（ページごとの HTML）。拡張ナビゲーション（faraNav）を入れて .site-main の中身を差し替えたら、
// 差し替えのあとに window.faraSite.pageChanged(backNavigation) を呼ぶ。各機能は init… 関数にまとめてあり、
// 差し替えたページに対して同じ関数を呼び直せば、そのページの初期化になる。
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
    const savePosition = (el) => {
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

    // ---- スマホのメニュー
    const initMenu = () => {
        const toggle = document.querySelector('.menu-toggle');
        const siteNav = document.getElementById('site-nav');
        if (!toggle || !siteNav) return;
        const menuOpen = () => toggle.getAttribute('aria-expanded') === 'true';
        const setMenu = (open) => {
            toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
            siteNav.classList.toggle('is-open', open);
            if (open) requestAnimationFrame(() => siteNav.querySelector('.nav-link')?.focus());
        };
        toggle.addEventListener('click', () => setMenu(!menuOpen()));
        document.addEventListener('click', (e) => {
            if (!menuOpen() || !(e.target instanceof Element)) return;
            if (e.target.closest('.site-nav, .menu-toggle')) return;
            setMenu(false);
        });
        document.addEventListener('keydown', (e) => {
            if (e.key !== 'Escape' || !menuOpen()) return;
            setMenu(false);
            toggle.focus();
        });
    };

    // ---- 更新履歴: 絞り込み（JS が無いときは、ボタンを出さずに全件を並べたままにする）
    const initUpdateFilter = () => {
        const filterBar = document.querySelector('[data-filter-bar]');
        if (!filterBar) return;
        filterBar.hidden = false;
        const buttons = filterBar.querySelectorAll('[data-filter]');
        const items = document.querySelectorAll('.update-item[data-category]');
        const groups = document.querySelectorAll('[data-year-group]');
        const empty = document.querySelector('[data-filter-empty]');
        const apply = (key) => {
            buttons.forEach((b) => {
                const on = b.dataset.filter === key;
                b.classList.toggle('is-active', on);
                b.setAttribute('aria-pressed', on ? 'true' : 'false');
            });
            let shown = 0;
            items.forEach((li) => {
                const show = key === 'all' || li.dataset.category === key;
                li.hidden = !show;
                if (show) shown++;
            });
            groups.forEach((g) => { g.hidden = !g.querySelector('.update-item:not([hidden])'); });
            if (empty) empty.hidden = shown > 0;
        };
        buttons.forEach((b) => b.addEventListener('click', () => apply(b.dataset.filter)));
    };

    // ---- 更新履歴: 更新から 14 日以内なら NEW
    const initNew = () => {
        const dayNumber = (y, m, d) => Date.UTC(y, m - 1, d) / 86400000;
        const now = new Date();
        const today = dayNumber(now.getFullYear(), now.getMonth() + 1, now.getDate());
        document.querySelectorAll('.update-item[data-date]').forEach((li) => {
            const [y, m, d] = li.dataset.date.split('-').map(Number);
            const diff = today - dayNumber(y, m, d);
            const chip = li.querySelector('[data-new]');
            if (chip && diff >= 0 && diff <= 14) chip.hidden = false;
        });
    };

    // ---- ページの表示（読み込みのあと、または拡張ナビゲーションで差し替えたあとに呼ぶ）
    //  #付きのときは、その場所へ。戻る・進むのときは、離れたときの位置へ。通常の移動は先頭へ
    const pageChanged = (backNavigation = false) => {
        flush();
        initReveal();
        const el = scrollEl();
        if (!el) return;
        if (location.hash) {
            scrollToHash();
            return;
        }
        el.scrollTo({ top: backNavigation ? (positions[pageKey()] || 0) : 0, behavior: 'instant' });
        update();
    };
    window.faraSite = { pageChanged, scrollToHash };

    // 初回の読み込み。機能ごとに、失敗しても他の機能は動くようにする
    const safely = (fn) => { try { fn(); } catch (e) { /* その機能だけ止まる。本文は読める */ } };
    safely(initOrbit);
    safely(initTooltips);
    safely(initMenu);
    safely(initUpdateFilter);
    safely(initNew);
    const navEntry = performance.getEntriesByType ? performance.getEntriesByType('navigation')[0] : null;
    safely(() => pageChanged(Boolean(navEntry && navEntry.type === 'back_forward')));
    safely(update);
})();

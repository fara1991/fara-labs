// サイト全体の演出（Blazor から独立して動く）
//  - スクロール量を CSS 変数 --progress に流す（上部の進捗バー）。スクロールするのは .site-scroll（ヘッダーの下の領域）
//  - .reveal 要素が画面に入ったら .is-visible を付ける（フェードイン）
//  - 軌道アイコンはタッチ中に止める／スマホのメニューを外側タップ・Esc で閉じる
//  - works#id で来たら該当の作品カードの枠を 1 回光らせる
//  - 戻る・進むで、離れたときのスクロール位置に戻す（同じページ内の移動は先頭へ）
(() => {
    const root = document.documentElement;
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    // スクロールするのは、ヘッダーとお知らせの帯の下の領域（Layout/MainLayout.razor の .site-scroll）。ページが読み込まれる前は無いので、都度探す
    const scrollEl = () => document.querySelector('.site-scroll');

    // 別のページに移ったあとの位置の保存と復元。保存先はメモリと sessionStorage（使えないときはメモリだけ）
    const STORE = 'faraScroll';
    let positions = {};
    // 読めた値が、位置の入ったオブジェクトでないとき（数値・文字列・配列など）は、空に戻す
    try {
        const parsed = JSON.parse(sessionStorage.getItem(STORE) || '{}');
        positions = parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : {};
    } catch (e) {
        positions = {};
    }
    // ページのキーはパスだけ（クエリやハッシュだけの違いは、同じページ。Layout/MainLayout.razor の _path と揃える）
    const pageKey = () => location.pathname;
    // いま画面に出しているページのキー（Blazor がページを切り替えたら、resetScroll で合わせる）
    let shownKey = pageKey();
    // 戻る・進むで、別のページへ移ったときだけ印を付ける（Blazor の処理より先に動く。このときの shownKey は、まだ移る前のページ）。
    //  同じページの中の移動（#やクエリだけの変化）では立てない。立てたままだと、あとの通常の移動が保存位置へ飛んでしまう
    let backNav = false;
    window.addEventListener('popstate', () => { backNav = pageKey() !== shownKey; });
    // 位置はメモリに置き、sessionStorage への書き込みは間引く（ページの切り替え・非表示・離脱のときにまとめて書く）
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
        // ページを切り替えている途中（アドレスは新しく、画面はまだ古い）の位置は、保存しない
        if (pageKey() !== shownKey) return;
        positions[shownKey] = el.scrollTop;
        dirty = true;
        if (!flushTimer) flushTimer = setTimeout(flush, 800);
    };
    window.addEventListener('pagehide', flush);
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'hidden') flush(); });

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
    update();

    // 画面に入ったかどうか。root は null（ビューポート）: 途中の祖先（.site-scroll）の枠で切られた部分は、見えていないと扱われる。
    // 領域を root に固定すると、ページの作り直しで領域が替わったあとも古い領域を見続けてしまうので、使わない
    const observer = new IntersectionObserver((entries) => {
        for (const entry of entries) {
            if (entry.isIntersecting) {
                entry.target.classList.add('is-visible');
                observer.unobserve(entry.target);
            }
        }
    }, { root: null, threshold: 0.1, rootMargin: '0px 0px -40px 0px' });

    const scan = (node) => {
        if (node.nodeType !== Node.ELEMENT_NODE) return;
        if (node.matches('.reveal:not(.is-visible)')) observer.observe(node);
        node.querySelectorAll('.reveal:not(.is-visible)').forEach((el) => observer.observe(el));
    };
    // Blazor が描画・ページ遷移で追加した要素も自動で監視する
    new MutationObserver((mutations) => {
        for (const m of mutations) m.addedNodes.forEach(scan);
        onScroll();
    }).observe(document.body, { childList: true, subtree: true });
    scan(document.body);
    root.classList.add('js-ready');

    // 軌道アイコンはタッチしている間とその直後は止めて、狙いやすくする
    let touchTimer = 0;
    document.addEventListener('pointerdown', (e) => {
        if (e.pointerType !== 'touch' || !(e.target instanceof Element)) return;
        const visual = e.target.closest('.hero-visual');
        if (!visual) return;
        visual.classList.add('is-touching');
        clearTimeout(touchTimer);
        touchTimer = setTimeout(() => visual.classList.remove('is-touching'), 4000);
    }, { passive: true });

    // スマホのメニュー: 開いたら最初のリンクへフォーカス、外側タップ・Esc で閉じる
    const menuToggle = () => document.querySelector('.menu-toggle');
    const menuOpen = () => menuToggle()?.getAttribute('aria-expanded') === 'true';
    document.addEventListener('click', (e) => {
        if (!menuOpen() || !(e.target instanceof Element)) return;
        if (e.target.closest('.site-nav, .menu-toggle')) return;
        menuToggle().click();
    });
    document.addEventListener('keydown', (e) => {
        if (e.key !== 'Escape' || !menuOpen()) return;
        menuToggle().click();
        menuToggle().focus();
    });
    let wasOpen = false;
    new MutationObserver(() => {
        const open = menuOpen();
        if (open && !wasOpen) requestAnimationFrame(() => document.querySelector('.site-nav .nav-link')?.focus());
        wasOpen = open;
    }).observe(document.body, { subtree: true, attributes: true, attributeFilter: ['aria-expanded'] });

    // トップの軌道アイコンの説明（.satellite-tip）が画面の左右からはみ出すときは内側に寄せる
    //  説明の位置は transform のアニメーション途中だと正しく測れないので、アイコンの中心と説明の幅から計算する
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

    window.faraSite = {
        // 別のページに移ったあと（Blazor が描き直したあと）に呼ぶ。戻る・進むなら保存した位置へ、通常の移動なら先頭へ
        resetScroll() {
            const back = backNav;
            backNav = false;
            flush();
            shownKey = pageKey();
            const el = scrollEl();
            // #ジャンプ（作品への移動・戻る先の作品など）は、どの移動でも scrollToHash に任せる（保存位置の復元と競合させない）
            if (!el || location.hash) return;
            // 戻る・進む: 離れたときの位置（保存が無ければ先頭）。通常の移動: 先頭
            el.scrollTo({ top: back ? (positions[shownKey] || 0) : 0, behavior: 'instant' });
        },
        scrollToHash() {
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
        },
    };
})();
